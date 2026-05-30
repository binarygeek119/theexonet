using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class BanAppealService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    PlayerBanService playerBanService,
    ILogger<BanAppealService> logger,
    MessageModerationService messageModerationService)
{
    public async Task<(BanAppealDto? Appeal, PlayerEntity? Player, PlayerBanDto? Ban, string? Error)> SubmitAppealAsync(
        string username,
        string password,
        string message,
        CancellationToken ct)
    {
        message = message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return (null, null, null, "Enter a message explaining why your ban should be removed.");
        }

        if (message.Length > 2000)
        {
            return (null, null, null, "Message cannot exceed 2000 characters.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Username == username.Trim(), ct);
        if (player is null || !passwordHasher.Verify(password, player.PasswordHash))
        {
            return (null, null, null, "Invalid username or password.");
        }

        var activeBan = await playerBanService.GetActiveBanAsync(player.Id, ct);
        if (activeBan is null)
        {
            return (null, null, null, "This account is not currently banned.");
        }

        var hasPending = await db.BanAppeals.AnyAsync(
            a => a.PlayerId == player.Id && a.Status == BanAppealStatuses.Pending,
            ct);
        if (hasPending)
        {
            return (null, null, null, "You already have a pending appeal. An admin will review it soon.");
        }

        var appeal = new BanAppealEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            BanId = activeBan.Id,
            Message = message,
            Status = BanAppealStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BanAppeals.Add(appeal);
        await db.SaveChangesAsync(ct);

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.BanAppeal,
            player.Username,
            "admins",
            appeal.Id,
            message);

        await messageModerationService.ScanAndFlagIfNeededAsync(
            MessageLogChannels.BanAppeal,
            appeal.Id,
            player.Id,
            player.Username,
            "admins",
            message,
            ct);

        return (MapAppeal(appeal, player, activeBan), player, activeBan, null);
    }

    public async Task<IReadOnlyList<BanAppealDto>> GetPendingAppealsAsync(CancellationToken ct)
    {
        var appeals = await db.BanAppeals.AsNoTracking()
            .Include(a => a.Player)
            .Where(a => a.Status == BanAppealStatuses.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var results = new List<BanAppealDto>();
        foreach (var appeal in appeals)
        {
            var activeBan = await playerBanService.GetActiveBanAsync(appeal.PlayerId, ct);
            results.Add(MapAppeal(appeal, appeal.Player, activeBan));
        }

        return results;
    }

    public async Task<(BanAppealDto? Appeal, string? Error)> DismissAppealAsync(
        Guid appealId,
        string staffUsername,
        CancellationToken ct)
    {
        var appeal = await db.BanAppeals
            .Include(a => a.Player)
            .FirstOrDefaultAsync(a => a.Id == appealId, ct);

        if (appeal is null)
        {
            return (null, "Appeal not found.");
        }

        if (appeal.Status != BanAppealStatuses.Pending)
        {
            return (null, "This appeal has already been reviewed.");
        }

        appeal.Status = BanAppealStatuses.Dismissed;
        appeal.ReviewedAt = DateTime.UtcNow;
        appeal.ReviewedByUsername = staffUsername.Trim();
        await db.SaveChangesAsync(ct);

        var activeBan = await playerBanService.GetActiveBanAsync(appeal.PlayerId, ct);
        return (MapAppeal(appeal, appeal.Player, activeBan), null);
    }

    private static BanAppealDto MapAppeal(
        BanAppealEntity appeal,
        PlayerEntity player,
        PlayerBanDto? activeBan) =>
        new(
            appeal.Id,
            appeal.PlayerId,
            player.Username,
            player.Email,
            appeal.Message,
            appeal.Status,
            appeal.CreatedAt,
            appeal.ReviewedAt,
            string.IsNullOrWhiteSpace(appeal.ReviewedByUsername) ? null : appeal.ReviewedByUsername,
            activeBan);
}
