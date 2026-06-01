using Microsoft.EntityFrameworkCore;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class AdminService(
    AppDbContext db,
    PlayerBanService playerBanService,
    StaffModerationPolicy staffModerationPolicy,
    PlayerWarningService playerWarningService,
    IGameCreditsConfig gameCreditsConfig)
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken ct)
    {
        var world = await db.GameWorld.AsNoTracking().FirstOrDefaultAsync(ct);
        return new AdminDashboardResponse(
            await db.Players.CountAsync(ct),
            await db.Mines.CountAsync(ct),
            await db.Friendships.CountAsync(f => f.Status == "accepted", ct),
            world?.CurrentDay ?? 1,
            await db.Players.SumAsync(p => p.Credits, ct),
            gameCreditsConfig.SignUp,
            gameCreditsConfig.BirthdayBonus);
    }

    public async Task<AdminPlayersResponse> GetPlayersAsync(string? search, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.Players.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Username, $"%{term}%")
                || EF.Functions.ILike(p.Email, $"%{term}%")
                || EF.Functions.ILike(p.ProfileNumber, $"%{term}%"));
        }

        var players = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new AdminPlayerSummary(
                p.Id,
                p.Username,
                p.Email,
                p.Credits,
                p.CreatedAt,
                p.Mines.Count))
            .ToListAsync(ct);

        var activeBans = await playerBanService.GetActiveBansForPlayersAsync(
            players.Select(p => p.Id).ToList(),
            ct);

        var enrichedPlayers = players
            .Select(player => player with
            {
                ActiveBan = activeBans.GetValueOrDefault(player.Id)
            })
            .ToList();

        return new AdminPlayersResponse(enrichedPlayers);
    }

    public async Task<(AdminPlayerSummary? Player, string? Error)> SetCreditsAsync(
        Guid playerId,
        decimal credits,
        CancellationToken ct)
    {
        if (credits < 0)
        {
            return (null, "Credits cannot be negative.");
        }

        var player = await db.Players
            .Include(p => p.Mines)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return (null, "Player not found.");
        }

        player.Credits = credits;
        await db.SaveChangesAsync(ct);

        return (new AdminPlayerSummary(
            player.Id,
            player.Username,
            player.Email,
            player.Credits,
            player.CreatedAt,
            player.Mines.Count,
            await playerBanService.GetActiveBanAsync(playerId, ct)), null);
    }

    public async Task<AdminPlayerProfileResponse?> GetPlayerProfileAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players
            .AsNoTracking()
            .Include(p => p.Mines)
            .ThenInclude(m => m.Workers)
            .Include(p => p.Mines)
            .ThenInclude(m => m.Zones)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return null;
        }

        var mine = player.Mines.FirstOrDefault(m => m.Status == MineStatus.Active)
            ?? player.Mines.MinBy(m => m.PurchasedAt);

        var activeFlag = await GetActiveFlagAsync(playerId, ct);
        var flagHistory = await GetFlagHistoryAsync(playerId, ct);
        var activeBan = await playerBanService.GetActiveBanAsync(playerId, ct);
        var banHistory = await playerBanService.GetBanHistoryAsync(playerId, ct);
        var isProtectedAdmin = staffModerationPolicy.IsProtectedAdmin(player.Username);
        var isModerator = staffModerationPolicy.IsModeratorAccount(player.Username);
        var warningHistory = await playerWarningService.GetWarningHistoryAsync(playerId, ct);
        var activeWarningCount = await playerWarningService.GetActiveWarningCountAsync(playerId, ct);

        return new AdminPlayerProfileResponse(
            player.Id,
            player.Username,
            player.Email,
            player.ProfileNumber,
            FormatProfileImageUrl(player.ProfileImageUrl, player.ProfileImageRevision),
            player.ProfileMood,
            player.ProfileAboutMe,
            player.ProfileMusic,
            player.ProfileInterests,
            player.ProfileDiscord,
            player.ProfileBluesky,
            player.ProfileTwitter,
            player.ProfileYoutube,
            player.ProfileFacebook,
            player.ProfileTheme,
            player.CreatedAt,
            player.Birthday?.ToString("yyyy-MM-dd"),
            player.LastBirthdayBonusYear,
            player.CurrentGameDay,
            player.Credits,
            mine?.Name ?? "No active mine",
            mine?.Workers.Count ?? 0,
            mine?.Zones.Count ?? 0,
            player.Mines.Count,
            activeFlag,
            flagHistory,
            activeBan,
            banHistory,
            isProtectedAdmin,
            isModerator,
            activeWarningCount,
            warningHistory);
    }

    public Task<(PlayerWarningDto? Warning, string? Error)> IssuePlayerWarningAsync(
        Guid playerId,
        string staffUsername,
        string reason,
        CancellationToken ct) =>
        playerWarningService.IssueWarningAsync(playerId, staffUsername, reason, null, ct);

    public Task<IReadOnlyList<BanLevelOptionDto>> GetBanLevelOptions() =>
        Task.FromResult<IReadOnlyList<BanLevelOptionDto>>(playerBanService.GetBanLevelOptions());

    public Task<(PlayerBanDto? Ban, string? Error)> SetPlayerBanAsync(
        Guid playerId,
        string banLevel,
        string bannedByUsername,
        string? reason,
        CancellationToken ct) =>
        playerBanService.SetBanAsync(playerId, banLevel, bannedByUsername, reason, ct);

    public Task<(PlayerBanDto? Ban, string? Error)> LiftPlayerBanAsync(
        Guid playerId,
        string actorUsername,
        CancellationToken ct) =>
        playerBanService.LiftBanAsync(playerId, actorUsername, ct);

    public async Task<(ProfileFlagDto? Flag, PlayerEntity? Player, string? Error)> FlagPlayerProfileAsync(
        Guid playerId,
        string flaggedByUsername,
        string comment,
        CancellationToken ct)
    {
        comment = comment.Trim();
        if (string.IsNullOrWhiteSpace(comment))
        {
            return (null, null, "A comment is required explaining why the profile was flagged.");
        }

        if (comment.Length > 2000)
        {
            return (null, null, "Comment cannot exceed 2000 characters.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null, "Player not found.");
        }

        var moderationError = staffModerationPolicy.ValidateModerationAction(
            player.Username,
            flaggedByUsername);
        if (moderationError is not null)
        {
            return (null, null, moderationError);
        }

        var now = DateTime.UtcNow;
        var activeFlags = await db.ProfileFlags
            .Where(f => f.PlayerId == playerId && f.ResolvedAt == null)
            .ToListAsync(ct);
        foreach (var existing in activeFlags)
        {
            existing.ResolvedAt = now;
        }

        var flag = new ProfileFlagEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            FlaggedByUsername = flaggedByUsername.Trim(),
            Comment = comment,
            CreatedAt = now
        };
        db.ProfileFlags.Add(flag);
        await db.SaveChangesAsync(ct);

        return (MapFlag(flag), player, null);
    }

    public async Task<ProfileFlagDto?> GetActiveFlagAsync(Guid playerId, CancellationToken ct)
    {
        var flag = await db.ProfileFlags.AsNoTracking()
            .Where(f => f.PlayerId == playerId && f.ResolvedAt == null)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return flag is null ? null : MapFlag(flag);
    }

    private async Task<IReadOnlyList<ProfileFlagDto>> GetFlagHistoryAsync(Guid playerId, CancellationToken ct)
    {
        var flags = await db.ProfileFlags.AsNoTracking()
            .Where(f => f.PlayerId == playerId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return flags.Select(MapFlag).ToList();
    }

    private static ProfileFlagDto MapFlag(ProfileFlagEntity flag) =>
        new(flag.Id, flag.FlaggedByUsername, flag.Comment, flag.CreatedAt, flag.ResolvedAt);

    private static string FormatProfileImageUrl(string url, int revision)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return revision > 0 ? $"{url}?v={revision}" : url;
    }
}
