using Microsoft.EntityFrameworkCore;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class SpecialEventService(AppDbContext db)
{
    public async Task<IReadOnlyList<LoginEventAnnouncementDto>> GetLoginAnnouncementsAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var claimedEventIds = await db.SpecialEventClaims
            .Where(c => c.PlayerId == playerId)
            .Select(c => c.EventId)
            .ToListAsync(ct);

        var announcedEventIds = await db.SpecialEventAnnouncements
            .Where(a => a.PlayerId == playerId)
            .Select(a => a.EventId)
            .ToListAsync(ct);

        var events = await db.SpecialEvents
            .Include(e => e.Rewards)
            .Where(e => e.IsActive)
            .Where(e => e.StartsAt == null || e.StartsAt <= now)
            .Where(e => e.EndsAt == null || e.EndsAt >= now)
            .Where(e => !claimedEventIds.Contains(e.Id))
            .Where(e => !announcedEventIds.Contains(e.Id))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return [];
        }

        foreach (var evt in events)
        {
            db.SpecialEventAnnouncements.Add(new SpecialEventAnnouncementEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                EventId = evt.Id,
                AnnouncedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        return events.Select(MapAnnouncement).ToList();
    }

    public async Task<IReadOnlyList<EventCompletionDto>> RecordProgressAsync(
        Guid playerId,
        SpecialEventChallengeType actionType,
        string? actionDetail,
        int increment = 1,
        CancellationToken ct = default)
    {
        if (increment <= 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var player = await db.Players
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return [];
        }

        var claimedEventIds = await db.SpecialEventClaims
            .Where(c => c.PlayerId == playerId)
            .Select(c => c.EventId)
            .ToListAsync(ct);

        var events = await db.SpecialEvents
            .Include(e => e.Rewards)
            .Where(e => e.IsActive)
            .Where(e => e.StartsAt == null || e.StartsAt <= now)
            .Where(e => e.EndsAt == null || e.EndsAt >= now)
            .Where(e => !claimedEventIds.Contains(e.Id))
            .Where(e => e.ChallengeType == actionType.ToString())
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return [];
        }

        var eventIds = events.Select(e => e.Id).ToList();
        var progressRows = await db.SpecialEventProgress
            .Where(p => p.PlayerId == playerId && eventIds.Contains(p.EventId))
            .ToDictionaryAsync(p => p.EventId, ct);

        var completions = new List<EventCompletionDto>();

        foreach (var evt in events)
        {
            if (!DetailMatches(evt.ChallengeDetail, actionDetail))
            {
                continue;
            }

            if (!progressRows.TryGetValue(evt.Id, out var progress))
            {
                progress = new SpecialEventProgressEntity
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    EventId = evt.Id,
                    ProgressCount = 0,
                    UpdatedAt = now
                };
                db.SpecialEventProgress.Add(progress);
                progressRows[evt.Id] = progress;
            }

            progress.ProgressCount += increment;
            progress.UpdatedAt = now;

            if (progress.ProgressCount < evt.ChallengeTarget)
            {
                continue;
            }

            var rewardGrants = GrantRewards(player, evt);
            db.SpecialEventClaims.Add(new SpecialEventClaimEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                EventId = evt.Id,
                ClaimedAt = now
            });

            completions.Add(new EventCompletionDto(
                evt.Id,
                evt.Title,
                $"Challenge complete! You won the {evt.Title} rewards.",
                rewardGrants));
        }

        if (progressRows.Count > 0 || completions.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return completions;
    }

    public async Task<ActiveMarketBonusesDto> GetActiveMarketBonusesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var events = await db.SpecialEvents
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Where(e => e.StartsAt == null || e.StartsAt <= now)
            .Where(e => e.EndsAt == null || e.EndsAt >= now)
            .Where(e => e.SaleBonusPercent > 0 || e.TradeBonusPercent > 0)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            return new ActiveMarketBonusesDto(0, 0, []);
        }

        return new ActiveMarketBonusesDto(
            events.Sum(e => e.SaleBonusPercent),
            events.Sum(e => e.TradeBonusPercent),
            events.Select(e => e.Title).ToList());
    }

    public async Task<SpecialEventsListResponse> ListAsync(CancellationToken ct)
    {
        var events = await db.SpecialEvents
            .Include(e => e.Rewards)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        var claimCounts = await db.SpecialEventClaims
            .GroupBy(c => c.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.EventId, g => g.Count, ct);

        return new SpecialEventsListResponse(events.Select(e => MapEvent(e, claimCounts.GetValueOrDefault(e.Id))).ToList());
    }

    public async Task<(SpecialEventDto? Event, string? Error)> CreateAsync(
        SaveSpecialEventRequest request,
        CancellationToken ct)
    {
        var (rewards, rewardError) = NormalizeRewards(
            request.Rewards,
            request.SaleBonusPercent,
            request.TradeBonusPercent);
        if (rewardError is not null)
        {
            return (null, rewardError);
        }

        var (challengeType, challengeError) = ParseChallengeType(request.ChallengeType, request.ChallengeTarget, request.ChallengeDetail);
        if (challengeError is not null)
        {
            return (null, challengeError);
        }

        var bonusError = ValidateMarketBonuses(request.SaleBonusPercent, request.TradeBonusPercent);
        if (bonusError is not null)
        {
            return (null, bonusError);
        }

        var validationError = ValidateEventFields(request.Title, request.Message, request.StartsAt, request.EndsAt);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        var now = DateTime.UtcNow;
        var evt = new SpecialEventEntity
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            IsActive = request.IsActive,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            ChallengeType = challengeType.ToString(),
            ChallengeTarget = request.ChallengeTarget,
            ChallengeDetail = NormalizeChallengeDetail(challengeType, request.ChallengeDetail),
            SaleBonusPercent = request.SaleBonusPercent,
            TradeBonusPercent = request.TradeBonusPercent,
            CreatedAt = now,
            UpdatedAt = now,
            Rewards = rewards
        };

        db.SpecialEvents.Add(evt);
        await db.SaveChangesAsync(ct);

        return (MapEvent(evt, 0), null);
    }

    public async Task<(SpecialEventDto? Event, string? Error)> UpdateAsync(
        Guid eventId,
        SaveSpecialEventRequest request,
        CancellationToken ct)
    {
        var evt = await db.SpecialEvents
            .Include(e => e.Rewards)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (evt is null)
        {
            return (null, "Event not found.");
        }

        var (rewards, rewardError) = NormalizeRewards(
            request.Rewards,
            request.SaleBonusPercent,
            request.TradeBonusPercent);
        if (rewardError is not null)
        {
            return (null, rewardError);
        }

        var (challengeType, challengeError) = ParseChallengeType(request.ChallengeType, request.ChallengeTarget, request.ChallengeDetail);
        if (challengeError is not null)
        {
            return (null, challengeError);
        }

        var bonusError = ValidateMarketBonuses(request.SaleBonusPercent, request.TradeBonusPercent);
        if (bonusError is not null)
        {
            return (null, bonusError);
        }

        var validationError = ValidateEventFields(request.Title, request.Message, request.StartsAt, request.EndsAt);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        evt.Title = request.Title.Trim();
        evt.Message = request.Message.Trim();
        evt.IsActive = request.IsActive;
        evt.StartsAt = request.StartsAt;
        evt.EndsAt = request.EndsAt;
        evt.ChallengeType = challengeType.ToString();
        evt.ChallengeTarget = request.ChallengeTarget;
        evt.ChallengeDetail = NormalizeChallengeDetail(challengeType, request.ChallengeDetail);
        evt.SaleBonusPercent = request.SaleBonusPercent;
        evt.TradeBonusPercent = request.TradeBonusPercent;
        evt.UpdatedAt = DateTime.UtcNow;
        db.SpecialEventRewards.RemoveRange(evt.Rewards);
        evt.Rewards = rewards;

        await db.SaveChangesAsync(ct);

        var claimCount = await db.SpecialEventClaims.CountAsync(c => c.EventId == eventId, ct);
        return (MapEvent(evt, claimCount), null);
    }

    public async Task<string?> DeleteAsync(Guid eventId, CancellationToken ct)
    {
        var evt = await db.SpecialEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (evt is null)
        {
            return "Event not found.";
        }

        db.SpecialEvents.Remove(evt);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public static string DescribeChallenge(SpecialEventChallengeType challengeType, int target, string detail)
    {
        return challengeType switch
        {
            SpecialEventChallengeType.AdvanceDay => target == 1
                ? "Advance the game by 1 day"
                : $"Advance the game by {target} days",
            SpecialEventChallengeType.SellOre => string.IsNullOrWhiteSpace(detail)
                ? target == 1 ? "Sell ore once" : $"Sell ore {target} times"
                : target == 1 ? $"Sell {detail} once" : $"Sell {detail} {target} times",
            SpecialEventChallengeType.BuySupply => string.IsNullOrWhiteSpace(detail)
                ? target == 1 ? "Buy supplies once" : $"Buy supplies {target} times"
                : target == 1 ? $"Buy {detail} once" : $"Buy {detail} {target} times",
            SpecialEventChallengeType.AssignWorker => target == 1
                ? "Assign a worker to a mining zone"
                : $"Assign workers to zones {target} times",
            _ => "Complete the special event challenge"
        };
    }

    public static string DescribeMarketBonuses(decimal saleBonusPercent, decimal tradeBonusPercent)
    {
        var parts = new List<string>();
        if (saleBonusPercent > 0)
        {
            parts.Add($"+{saleBonusPercent:0.##}% bonus credits on ore sales");
        }

        if (tradeBonusPercent > 0)
        {
            parts.Add($"+{tradeBonusPercent:0.##}% bonus credits back on supply purchases");
        }

        return parts.Count == 0 ? string.Empty : string.Join("; ", parts);
    }

    private static LoginEventAnnouncementDto MapAnnouncement(SpecialEventEntity evt)
    {
        if (!Enum.TryParse<SpecialEventChallengeType>(evt.ChallengeType, out var challengeType))
        {
            challengeType = SpecialEventChallengeType.AdvanceDay;
        }

        return new LoginEventAnnouncementDto(
            evt.Id,
            evt.Title,
            evt.Message,
            challengeType.ToString(),
            evt.ChallengeTarget,
            evt.ChallengeDetail,
            DescribeChallenge(challengeType, evt.ChallengeTarget, evt.ChallengeDetail),
            evt.SaleBonusPercent,
            evt.TradeBonusPercent,
            DescribeMarketBonuses(evt.SaleBonusPercent, evt.TradeBonusPercent),
            evt.Rewards
                .OrderBy(r => r.ItemType)
                .Select(r => new EventRewardDto(r.ItemType, r.Amount))
                .ToList());
    }

    private List<EventRewardGrantDto> GrantRewards(PlayerEntity player, SpecialEventEntity evt)
    {
        var rewardGrants = new List<EventRewardGrantDto>();

        foreach (var reward in evt.Rewards)
        {
            var parsed = ParseRewardItem(reward.ItemType);
            if (parsed.Error is not null || reward.Amount <= 0)
            {
                continue;
            }

            if (parsed.IsCredits)
            {
                player.Credits += reward.Amount;
                db.Transactions.Add(new TransactionEntity
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = TransactionType.SpecialEventBonus,
                    Amount = reward.Amount,
                    Description = $"Special event: {evt.Title}",
                    GameDay = player.CurrentGameDay
                });
                rewardGrants.Add(new EventRewardGrantDto("Credits", "Credits", reward.Amount));
                continue;
            }

            if (parsed.Category is null)
            {
                continue;
            }

            var item = player.Inventory.FirstOrDefault(i =>
                i.Category == parsed.Category &&
                i.ItemType == parsed.NormalizedType);

            if (item is null)
            {
                item = new InventoryItemEntity
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Category = parsed.Category.Value,
                    ItemType = parsed.NormalizedType,
                    Quantity = reward.Amount
                };
                db.Inventory.Add(item);
                player.Inventory.Add(item);
            }
            else
            {
                item.Quantity += reward.Amount;
            }

            rewardGrants.Add(new EventRewardGrantDto(
                parsed.NormalizedType,
                parsed.Category.Value.ToString(),
                reward.Amount));
        }

        return rewardGrants;
    }

    private static bool DetailMatches(string eventDetail, string? actionDetail)
    {
        if (string.IsNullOrWhiteSpace(eventDetail))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actionDetail))
        {
            return false;
        }

        return string.Equals(eventDetail.Trim(), actionDetail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidateEventFields(
        string title,
        string message,
        DateTime? startsAt,
        DateTime? endsAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Event title is required.";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Event message is required.";
        }

        if (startsAt.HasValue && endsAt.HasValue && endsAt < startsAt)
        {
            return "End date must be after the start date.";
        }

        return null;
    }

    private static (SpecialEventChallengeType Type, string? Error) ParseChallengeType(
        string challengeType,
        int challengeTarget,
        string? challengeDetail)
    {
        if (challengeTarget < 1)
        {
            return (default, "Challenge target must be at least 1.");
        }

        if (!Enum.TryParse<SpecialEventChallengeType>(challengeType, ignoreCase: true, out var parsed))
        {
            return (default, "Invalid challenge type.");
        }

        if (parsed is SpecialEventChallengeType.SellOre &&
            !string.IsNullOrWhiteSpace(challengeDetail) &&
            !Enum.TryParse<OreType>(challengeDetail, ignoreCase: true, out _))
        {
            return (default, "Challenge detail must be a valid ore type or left blank for any ore.");
        }

        if (parsed is SpecialEventChallengeType.BuySupply &&
            !string.IsNullOrWhiteSpace(challengeDetail) &&
            !Enum.TryParse<SupplyType>(challengeDetail, ignoreCase: true, out _))
        {
            return (default, "Challenge detail must be a valid supply type or left blank for any supply.");
        }

        return (parsed, null);
    }

    private static string NormalizeChallengeDetail(SpecialEventChallengeType challengeType, string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        return challengeType switch
        {
            SpecialEventChallengeType.SellOre when Enum.TryParse<OreType>(detail, ignoreCase: true, out var ore) => ore.ToString(),
            SpecialEventChallengeType.BuySupply when Enum.TryParse<SupplyType>(detail, ignoreCase: true, out var supply) => supply.ToString(),
            _ => string.Empty
        };
    }

    private static string? ValidateMarketBonuses(decimal saleBonusPercent, decimal tradeBonusPercent)
    {
        if (saleBonusPercent < 0 || tradeBonusPercent < 0)
        {
            return "Market bonus percentages cannot be negative.";
        }

        if (saleBonusPercent > 100 || tradeBonusPercent > 100)
        {
            return "Market bonus percentages cannot exceed 100%.";
        }

        return null;
    }

    private static (List<SpecialEventRewardEntity> Rewards, string? Error) NormalizeRewards(
        IReadOnlyList<EventRewardDto>? rewards,
        decimal saleBonusPercent,
        decimal tradeBonusPercent)
    {
        if (rewards is null || rewards.Count == 0)
        {
            if (saleBonusPercent <= 0 && tradeBonusPercent <= 0)
            {
                return ([], "Add at least one challenge reward or a sale/trade bonus.");
            }

            return ([], null);
        }

        var normalized = new List<SpecialEventRewardEntity>();
        foreach (var reward in rewards)
        {
            var parsed = ParseRewardItem(reward.ItemType);
            if (parsed.Error is not null)
            {
                return ([], parsed.Error);
            }

            if (reward.Amount <= 0)
            {
                return ([], "Reward amounts must be greater than zero.");
            }

            normalized.Add(new SpecialEventRewardEntity
            {
                Id = Guid.NewGuid(),
                ItemType = parsed.NormalizedType,
                Amount = reward.Amount
            });
        }

        return (normalized, null);
    }

    private static SpecialEventDto MapEvent(SpecialEventEntity evt, int claimCount) =>
        new(
            evt.Id,
            evt.Title,
            evt.Message,
            evt.IsActive,
            evt.StartsAt,
            evt.EndsAt,
            evt.ChallengeType,
            evt.ChallengeTarget,
            evt.ChallengeDetail,
            DescribeChallenge(
                Enum.TryParse<SpecialEventChallengeType>(evt.ChallengeType, out var type)
                    ? type
                    : SpecialEventChallengeType.AdvanceDay,
                evt.ChallengeTarget,
                evt.ChallengeDetail),
            evt.SaleBonusPercent,
            evt.TradeBonusPercent,
            DescribeMarketBonuses(evt.SaleBonusPercent, evt.TradeBonusPercent),
            evt.Rewards
                .OrderBy(r => r.ItemType)
                .Select(r => new EventRewardDto(r.ItemType, r.Amount))
                .ToList(),
            evt.CreatedAt,
            evt.UpdatedAt,
            claimCount);

    private sealed record ParsedRewardItem(
        bool IsCredits,
        ItemCategory? Category,
        string NormalizedType,
        string? Error);

    private static ParsedRewardItem ParseRewardItem(string itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType))
        {
            return new ParsedRewardItem(false, null, string.Empty, "Item type is required.");
        }

        var trimmed = itemType.Trim();
        if (string.Equals(trimmed, "Credits", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedRewardItem(true, null, "Credits", null);
        }

        if (Enum.TryParse<OreType>(trimmed, ignoreCase: true, out var ore))
        {
            return new ParsedRewardItem(false, ItemCategory.Ore, ore.ToString(), null);
        }

        if (Enum.TryParse<SupplyType>(trimmed, ignoreCase: true, out var supply))
        {
            return new ParsedRewardItem(false, ItemCategory.Supply, supply.ToString(), null);
        }

        return new ParsedRewardItem(
            false,
            null,
            string.Empty,
            "Unknown item type. Use Credits, an ore (Ferroxite, Voidium, Stellarite, SalvageScrap), or a supply (DrillBits, FuelCells, LifeSupport, CommModules).");
    }
}
