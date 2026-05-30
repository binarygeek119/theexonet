using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class PlayerWarningService(AppDbContext db, StaffModerationPolicy staffModerationPolicy)
{
    public async Task<int> GetActiveWarningCountAsync(Guid playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.PlayerWarnings.AsNoTracking()
            .CountAsync(w => w.PlayerId == playerId && w.ExpiresAt > now, ct);
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
            ExpiresAt = now.AddDays(ModerationWarningLimits.WarningDurationDays)
        };

        db.PlayerWarnings.Add(warning);
        await db.SaveChangesAsync(ct);

        return (MapWarning(warning), null);
    }

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
            warning.ExpiresAt > now);
    }
}
