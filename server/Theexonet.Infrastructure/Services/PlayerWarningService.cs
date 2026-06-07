using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class PlayerWarningService(
    AppDbContext db,
    StaffModerationPolicy staffModerationPolicy,
    IPlayerModerationNotifier moderationNotifier,
    ILogger<PlayerWarningService> logger)
{
    public async Task<int> GetActiveWarningCountAsync(Guid playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.PlayerWarnings.AsNoTracking()
            .CountAsync(w => w.PlayerId == playerId && w.ExpiresAt > now, ct);
    }

    public async Task<IReadOnlyList<PlayerModerationWarningDto>> GetUnacknowledgedWarningsAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var warnings = await db.PlayerWarnings.AsNoTracking()
            .Where(w => w.PlayerId == playerId && w.ExpiresAt > now && w.AcknowledgedAt == null)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

        return warnings.Select(MapModerationWarning).ToList();
    }

    public async Task<IReadOnlyList<PlayerWarningDto>> GetWarningHistoryAsync(Guid playerId, CancellationToken ct)
    {
        var warnings = await db.PlayerWarnings.AsNoTracking()
            .Where(w => w.PlayerId == playerId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return warnings.Select(MapWarning).ToList();
    }

    public async Task<(bool Success, string? Error)> AcknowledgeWarningAsync(
        Guid warningId,
        Guid playerId,
        CancellationToken ct)
    {
        var warning = await db.PlayerWarnings
            .FirstOrDefaultAsync(w => w.Id == warningId && w.PlayerId == playerId, ct);
        if (warning is null)
        {
            return (false, "Warning not found.");
        }

        if (warning.AcknowledgedAt is not null)
        {
            return (true, null);
        }

        var now = DateTime.UtcNow;
        if (warning.ExpiresAt <= now)
        {
            return (false, "This warning has expired.");
        }

        warning.AcknowledgedAt = now;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(PlayerWarningDto? Warning, string? Error)> IssueWarningAsync(
        Guid playerId,
        string staffUsername,
        string reason,
        Guid? flaggedMessageId,
        CancellationToken ct)
    {
        reason = reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (null, "A reason is required for the warning.");
        }

        if (reason.Length > 2000)
        {
            return (null, "Reason cannot exceed 2000 characters.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, "Player not found.");
        }

        var moderationError = staffModerationPolicy.ValidateModerationAction(
            player.Username,
            staffUsername);
        if (moderationError is not null)
        {
            return (null, moderationError);
        }

        var activeCount = await GetActiveWarningCountAsync(playerId, ct);
        if (activeCount >= ModerationWarningLimits.MaxWarningsBeforeBan)
        {
            return (null, $"Player already has {ModerationWarningLimits.MaxWarningsBeforeBan} active warnings. Issue a ban instead.");
        }

        if (flaggedMessageId.HasValue)
        {
            var flagExists = await db.FlaggedMessages.AsNoTracking()
                .AnyAsync(f => f.Id == flaggedMessageId.Value, ct);
            if (!flagExists)
            {
                return (null, "Flagged message not found.");
            }
        }

        var now = DateTime.UtcNow;
        var warning = new PlayerWarningEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            FlaggedMessageId = flaggedMessageId,
            Reason = reason,
            IssuedByUsername = staffUsername.Trim(),
            CreatedAt = now,
            ExpiresAt = now.AddDays(ModerationWarningLimits.WarningDurationDays),
            AcknowledgedAt = null
        };

        db.PlayerWarnings.Add(warning);
        await db.SaveChangesAsync(ct);

        var warningDto = MapWarning(warning);
        await TryNotifyWarningAsync(player, warningDto, ct);
        return (warningDto, null);
    }

    private async Task TryNotifyWarningAsync(PlayerEntity player, PlayerWarningDto warning, CancellationToken ct)
    {
        if (ModerationEmailPolicy.ShouldSkipNotification(warning.Reason)
            || string.IsNullOrWhiteSpace(player.Email))
        {
            return;
        }

        try
        {
            await moderationNotifier.NotifyWarningAsync(player.Email, player.Username, warning, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Account warning email failed for {Username} ({Email})",
                player.Username,
                player.Email);
        }
    }

    public static string FormatWarningMessage(PlayerModerationWarningDto warning) =>
        $"You have received an account warning. Reason: {warning.Reason.Trim()}";

    public static PlayerModerationWarningDto MapModerationWarning(PlayerWarningEntity warning) =>
        new(
            warning.Id,
            warning.Reason,
            warning.IssuedByUsername,
            warning.CreatedAt,
            warning.ExpiresAt);

    public static PlayerWarningDto MapWarning(PlayerWarningEntity warning)
    {
        var now = DateTime.UtcNow;
        return new(
            warning.Id,
            warning.FlaggedMessageId,
            warning.Reason,
            warning.IssuedByUsername,
            warning.CreatedAt,
            warning.ExpiresAt,
            warning.ExpiresAt > now,
            warning.AcknowledgedAt is not null);
    }
}
