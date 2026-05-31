using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class MessageModerationService(
    AppDbContext db,
    HateSpeechScanner hateSpeechScanner,
    StaffModerationPolicy staffModerationPolicy,
    PlayerBanService playerBanService,
    PlayerWarningService playerWarningService)
{
    public async Task ScanAndFlagIfNeededAsync(
        string channel,
        Guid sourceMessageId,
        Guid playerId,
        string fromLabel,
        string toLabel,
        string body,
        CancellationToken ct)
    {
        var (isMatch, matchedTerms) = hateSpeechScanner.Scan(body);
        if (!isMatch)
        {
            return;
        }

        var username = await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        if (staffModerationPolicy.IsProtectedAdmin(username)
            || staffModerationPolicy.IsModeratorAccount(username))
        {
            return;
        }

        var flag = new FlaggedMessageEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Channel = channel,
            SourceMessageId = sourceMessageId,
            FromLabel = fromLabel,
            ToLabel = toLabel,
            Body = body,
            MatchedTerms = matchedTerms,
            Status = FlaggedMessageStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.FlaggedMessages.Add(flag);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct) =>
        await db.FlaggedMessages.AsNoTracking()
            .CountAsync(m => m.Status == FlaggedMessageStatuses.Pending, ct);

    public async Task<IReadOnlyList<FlaggedMessageReviewDto>> GetPendingReviewsAsync(CancellationToken ct)
    {
        var flags = await db.FlaggedMessages.AsNoTracking()
            .Where(m => m.Status == FlaggedMessageStatuses.Pending)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return await MapReviewsAsync(flags, ct);
    }

    public async Task<(FlaggedMessageReviewDto? Review, string? Error)> DismissAsync(
        Guid flaggedMessageId,
        string staffUsername,
        CancellationToken ct)
    {
        var flag = await db.FlaggedMessages.FirstOrDefaultAsync(m => m.Id == flaggedMessageId, ct);
        if (flag is null)
        {
            return (null, "Flagged message not found.");
        }

        if (flag.Status != FlaggedMessageStatuses.Pending)
        {
            return (null, "This message has already been reviewed.");
        }

        flag.Status = FlaggedMessageStatuses.Dismissed;
        flag.ReviewedAt = DateTime.UtcNow;
        flag.ReviewedByUsername = staffUsername.Trim();
        await db.SaveChangesAsync(ct);

        var reviews = await MapReviewsAsync([flag], ct);
        return (reviews[0], null);
    }

    public async Task<(PlayerWarningDto? Warning, FlaggedMessageReviewDto? Review, string? Error)> IssueWarningAsync(
        Guid flaggedMessageId,
        string staffUsername,
        CancellationToken ct)
    {
        var flag = await db.FlaggedMessages.FirstOrDefaultAsync(m => m.Id == flaggedMessageId, ct);
        if (flag is null)
        {
            return (null, null, "Flagged message not found.");
        }

        if (flag.Status != FlaggedMessageStatuses.Pending)
        {
            return (null, null, "This message has already been reviewed.");
        }

        if (flag.PlayerId is null)
        {
            return (null, null, "This flagged message is not linked to a player account.");
        }

        var (warning, error) = await playerWarningService.IssueWarningAsync(
            flag.PlayerId.Value,
            staffUsername,
            "Hate speech, bad language, political, or sexual terms in message",
            flag.Id,
            ct);

        if (error is not null)
        {
            return (null, null, error);
        }

        flag.Status = FlaggedMessageStatuses.Confirmed;
        flag.ReviewedAt = DateTime.UtcNow;
        flag.ReviewedByUsername = staffUsername.Trim();
        await db.SaveChangesAsync(ct);

        var reviews = await MapReviewsAsync([flag], ct);
        return (warning, reviews[0], null);
    }

    public async Task<(PlayerBanDto? Ban, FlaggedMessageReviewDto? Review, string? Error)> IssueBanAsync(
        Guid flaggedMessageId,
        string staffUsername,
        string banLevel,
        string? reason,
        CancellationToken ct)
    {
        var flag = await db.FlaggedMessages.FirstOrDefaultAsync(m => m.Id == flaggedMessageId, ct);
        if (flag is null)
        {
            return (null, null, "Flagged message not found.");
        }

        if (flag.Status != FlaggedMessageStatuses.Pending)
        {
            return (null, null, "This message has already been reviewed.");
        }

        if (flag.PlayerId is null)
        {
            return (null, null, "This flagged message is not linked to a player account.");
        }

        var warningCount = await playerWarningService.GetActiveWarningCountAsync(flag.PlayerId.Value, ct);
        if (warningCount < ModerationWarningLimits.MaxWarningsBeforeBan)
        {
            return (null, null, $"Player must have {ModerationWarningLimits.MaxWarningsBeforeBan} active warnings before a ban can be issued.");
        }

        var banReason = string.IsNullOrWhiteSpace(reason)
            ? "Hate speech, bad language, political, or sexual terms in message"
            : reason.Trim();

        var (ban, banError) = await playerBanService.SetBanAsync(
            flag.PlayerId.Value,
            banLevel,
            staffUsername,
            banReason,
            ct);

        if (banError is not null)
        {
            return (null, null, banError);
        }

        flag.Status = FlaggedMessageStatuses.Confirmed;
        flag.ReviewedAt = DateTime.UtcNow;
        flag.ReviewedByUsername = staffUsername.Trim();
        await db.SaveChangesAsync(ct);

        var reviews = await MapReviewsAsync([flag], ct);
        return (ban, reviews[0], null);
    }

    private async Task<IReadOnlyList<FlaggedMessageReviewDto>> MapReviewsAsync(
        IReadOnlyList<FlaggedMessageEntity> flags,
        CancellationToken ct)
    {
        if (flags.Count == 0)
        {
            return [];
        }

        var playerIds = flags
            .Where(flag => flag.PlayerId.HasValue)
            .Select(flag => flag.PlayerId!.Value)
            .Distinct()
            .ToList();

        var usernames = playerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Players.AsNoTracking()
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

        var warningsByPlayer = new Dictionary<Guid, List<PlayerWarningDto>>();
        var activeCountsByPlayer = new Dictionary<Guid, int>();
        foreach (var playerId in playerIds)
        {
            warningsByPlayer[playerId] = (await playerWarningService.GetWarningHistoryAsync(playerId, ct)).ToList();
            activeCountsByPlayer[playerId] = await playerWarningService.GetActiveWarningCountAsync(playerId, ct);
        }

        return flags.Select(flag =>
        {
            var playerUsername = flag.PlayerId.HasValue
                ? usernames.GetValueOrDefault(flag.PlayerId.Value, "Unknown")
                : flag.FromLabel;
            var warnings = flag.PlayerId.HasValue
                ? warningsByPlayer.GetValueOrDefault(flag.PlayerId.Value, [])
                : [];
            var activeCount = flag.PlayerId.HasValue
                ? activeCountsByPlayer.GetValueOrDefault(flag.PlayerId.Value, 0)
                : 0;
            return new FlaggedMessageReviewDto(
                flag.Id,
                flag.PlayerId,
                playerUsername,
                flag.Channel,
                flag.SourceMessageId,
                flag.FromLabel,
                flag.ToLabel,
                flag.Body,
                flag.MatchedTerms,
                flag.Status,
                flag.CreatedAt,
                string.IsNullOrWhiteSpace(flag.ReviewedByUsername) ? null : flag.ReviewedByUsername,
                flag.ReviewedAt,
                activeCount,
                warnings);
        }).ToList();
    }
}
