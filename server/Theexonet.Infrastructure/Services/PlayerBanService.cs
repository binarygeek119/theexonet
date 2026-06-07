using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class PlayerBanService(
    AppDbContext db,
    StaffModerationPolicy staffModerationPolicy,
    IOptionsMonitor<AdminOptions> adminOptions,
    IPlayerModerationNotifier moderationNotifier,
    ILogger<PlayerBanService> logger)
{
    public IReadOnlyList<BanLevelOptionDto> GetBanLevelOptions() =>
        BanLevels.Options
            .Select(option => new BanLevelOptionDto(option.Code, option.Label))
            .ToList();

    public IReadOnlyList<string> GetBanReasonPresets() => BanReasons.Presets;

    public async Task<PlayerBanDto?> GetActiveBanAsync(Guid playerId, CancellationToken ct)
    {
        if (await IsProtectedAdminAsync(playerId, ct))
        {
            return null;
        }

        var ban = await QueryActiveBans()
            .Where(b => b.PlayerId == playerId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return ban is null ? null : MapBan(ban);
    }

    public async Task<IReadOnlyDictionary<Guid, PlayerBanDto>> GetActiveBansForPlayersAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken ct)
    {
        if (playerIds.Count == 0)
        {
            return new Dictionary<Guid, PlayerBanDto>();
        }

        var bans = await QueryActiveBans()
            .Where(b => playerIds.Contains(b.PlayerId))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        if (bans.Count == 0)
        {
            return new Dictionary<Guid, PlayerBanDto>();
        }

        var protectedPlayerIds = await GetProtectedAdminPlayerIdsAsync(playerIds, ct);

        return bans
            .Where(b => !protectedPlayerIds.Contains(b.PlayerId))
            .GroupBy(b => b.PlayerId)
            .ToDictionary(group => group.Key, group => MapBan(group.First()));
    }

    public async Task<IReadOnlyList<PlayerBanDto>> GetBanHistoryAsync(Guid playerId, CancellationToken ct)
    {
        var bans = await db.PlayerBans.AsNoTracking()
            .Where(b => b.PlayerId == playerId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return bans.Select(MapBan).ToList();
    }

    public async Task<AdminBansResponse> GetAdminBanListAsync(
        string? search,
        bool activeOnly,
        int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        var now = DateTime.UtcNow;
        var protectedPlayerIds = await db.Players.AsNoTracking()
            .Select(p => new { p.Id, p.Username })
            .ToListAsync(ct);
        var excludedPlayerIds = protectedPlayerIds
            .Where(p => adminOptions.CurrentValue.IsAdminUsername(p.Username))
            .Select(p => p.Id)
            .ToList();

        var query = db.PlayerBans.AsNoTracking()
            .Include(b => b.Player)
            .AsQueryable();

        if (excludedPlayerIds.Count > 0)
        {
            query = query.Where(b => !excludedPlayerIds.Contains(b.PlayerId));
        }

        if (activeOnly)
        {
            query = query.Where(b => b.LiftedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b =>
                EF.Functions.ILike(b.Player.Username, $"%{term}%")
                || EF.Functions.ILike(b.Player.Email, $"%{term}%")
                || EF.Functions.ILike(b.Player.ProfileNumber, $"%{term}%")
                || EF.Functions.ILike(b.Reason, $"%{term}%")
                || EF.Functions.ILike(b.BannedByUsername, $"%{term}%"));
        }

        var bans = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var items = bans
            .Select(b => new AdminBanListItemDto(
                b.Id,
                b.PlayerId,
                b.Player.Username,
                b.Player.Email,
                b.Player.ProfileNumber,
                MapBan(b)))
            .ToList();

        return new AdminBansResponse(items);
    }

    public async Task<string?> GetActiveBanMessageAsync(Guid playerId, CancellationToken ct)
    {
        var ban = await GetActiveBanAsync(playerId, ct);
        return ban is null ? null : FormatMessage(ban);
    }

    public async Task<(PlayerBanDto? Ban, string? Error)> SetBanAsync(
        Guid playerId,
        string banLevel,
        string bannedByUsername,
        string? reason,
        CancellationToken ct)
    {
        if (!BanLevels.TryParse(banLevel, out var normalizedLevel))
        {
            return (null, "Select a valid ban level.");
        }

        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            return (null, "Enter a ban reason for the player.");
        }

        if (reason.Length > 2000)
        {
            return (null, "Ban reason cannot exceed 2000 characters.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, "Player not found.");
        }

        var moderationError = staffModerationPolicy.ValidateModerationAction(
            player.Username,
            bannedByUsername);
        if (moderationError is not null)
        {
            return (null, moderationError);
        }

        var now = DateTime.UtcNow;
        var activeBans = await db.PlayerBans
            .Where(b => b.PlayerId == playerId && b.LiftedAt == null &&
                        (b.ExpiresAt == null || b.ExpiresAt > now))
            .ToListAsync(ct);

        foreach (var existing in activeBans)
        {
            existing.LiftedAt = now;
        }

        var ban = new PlayerBanEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            BanLevel = normalizedLevel,
            BannedByUsername = bannedByUsername.Trim(),
            Reason = reason,
            CreatedAt = now,
            ExpiresAt = BanLevels.CalculateExpiresAt(normalizedLevel, now)
        };

        db.PlayerBans.Add(ban);
        await db.SaveChangesAsync(ct);

        var banDto = MapBan(ban);
        await TryNotifyBanAsync(player, banDto, ct);
        return (banDto, null);
    }

    private async Task TryNotifyBanAsync(PlayerEntity player, PlayerBanDto ban, CancellationToken ct)
    {
        if (ModerationEmailPolicy.ShouldSkipNotification(ban.Reason)
            || string.IsNullOrWhiteSpace(player.Email))
        {
            return;
        }

        try
        {
            await moderationNotifier.NotifyBanAsync(player.Email, player.Username, ban, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Account ban email failed for {Username} ({Email})",
                player.Username,
                player.Email);
        }
    }

    public async Task<(PlayerBanDto? Ban, string? Error)> LiftBanAsync(
        Guid playerId,
        string actorUsername,
        CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, "Player not found.");
        }

        var moderationError = staffModerationPolicy.ValidateModerationAction(
            player.Username,
            actorUsername);
        if (moderationError is not null)
        {
            return (null, moderationError);
        }

        var now = DateTime.UtcNow;
        var activeBan = await db.PlayerBans
            .Where(b => b.PlayerId == playerId && b.LiftedAt == null &&
                        (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (activeBan is null)
        {
            return (null, "Player is not currently banned.");
        }

        activeBan.LiftedAt = now;
        await db.SaveChangesAsync(ct);

        return (MapBan(activeBan), null);
    }

    public static string FormatMessage(PlayerBanDto ban) =>
        BanLevels.FormatBanMessage(ban.BanLevel, ban.ExpiresAt, ban.Reason);

    private async Task<bool> IsProtectedAdminAsync(Guid playerId, CancellationToken ct)
    {
        var username = await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct);

        return username is not null && adminOptions.CurrentValue.IsAdminUsername(username);
    }

    private async Task<HashSet<Guid>> GetProtectedAdminPlayerIdsAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken ct)
    {
        if (playerIds.Count == 0)
        {
            return [];
        }

        var players = await db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Username })
            .ToListAsync(ct);

        return players
            .Where(p => adminOptions.CurrentValue.IsAdminUsername(p.Username))
            .Select(p => p.Id)
            .ToHashSet();
    }

    private IQueryable<PlayerBanEntity> QueryActiveBans()
    {
        var now = DateTime.UtcNow;
        return db.PlayerBans.AsNoTracking()
            .Where(b => b.LiftedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now));
    }

    private static PlayerBanDto MapBan(PlayerBanEntity ban)
    {
        var now = DateTime.UtcNow;
        var isActive = ban.LiftedAt is null && (ban.ExpiresAt is null || ban.ExpiresAt > now);

        return new PlayerBanDto(
            ban.Id,
            ban.BanLevel,
            BanLevels.GetLabel(ban.BanLevel),
            ban.BannedByUsername,
            string.IsNullOrWhiteSpace(ban.Reason) ? null : ban.Reason,
            ban.CreatedAt,
            ban.ExpiresAt,
            BanLevels.IsPermanent(ban.BanLevel),
            isActive,
            ban.LiftedAt);
    }
}
