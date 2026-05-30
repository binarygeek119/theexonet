using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class PlayerMessageService(AppDbContext db, ILogger<PlayerMessageService> logger)
{
    public async Task<IReadOnlyList<PlayerMessageDto>> GetInboxAsync(Guid playerId, CancellationToken ct)
    {
        var messages = await db.PlayerMessages.AsNoTracking()
            .Where(m => m.PlayerId == playerId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return messages.Select(MapMessage).ToList();
    }

    public async Task<IReadOnlyList<PlayerMessageDto>> GetSentToPlayerAsync(Guid playerId, CancellationToken ct)
    {
        var messages = await db.PlayerMessages.AsNoTracking()
            .Where(m => m.PlayerId == playerId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return messages.Select(MapMessage).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid playerId, CancellationToken ct) =>
        await db.PlayerMessages.AsNoTracking()
            .CountAsync(m => m.PlayerId == playerId && m.ReadAt == null, ct);

    public async Task<(PlayerMessageDto? Message, string? Error)> SendToPlayerAsync(
        string fromStaffUsername,
        Guid playerId,
        string body,
        CancellationToken ct)
    {
        body = body.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, "Message cannot be empty.");
        }

        if (body.Length > 4000)
        {
            return (null, "Message cannot exceed 4000 characters.");
        }

        var playerExists = await db.Players.AsNoTracking()
            .AnyAsync(p => p.Id == playerId, ct);
        if (!playerExists)
        {
            return (null, "Player not found.");
        }

        var message = new PlayerMessageEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            FromStaffUsername = fromStaffUsername.Trim(),
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        db.PlayerMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var playerUsername = await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct) ?? playerId.ToString();

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.StaffToPlayer,
            fromStaffUsername.Trim(),
            playerUsername,
            message.Id,
            body);

        return (MapMessage(message), null);
    }

    public async Task<(PlayerMessageDto? Message, string? Error)> MarkReadAsync(
        Guid messageId,
        Guid playerId,
        CancellationToken ct)
    {
        var message = await db.PlayerMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return (null, "Message not found.");
        }

        if (message.PlayerId != playerId)
        {
            return (null, "You can only mark your own messages as read.");
        }

        if (message.ReadAt is null)
        {
            message.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return (MapMessage(message), null);
    }

    private static PlayerMessageDto MapMessage(PlayerMessageEntity message) =>
        new(
            message.Id,
            message.FromStaffUsername,
            message.Body,
            message.CreatedAt,
            message.ReadAt,
            message.ReadAt is not null);
}
