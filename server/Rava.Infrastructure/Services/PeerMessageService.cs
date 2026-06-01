using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class PeerMessageService(
    AppDbContext db,
    ILogger<PeerMessageService> logger,
    MessageModerationService messageModerationService)
{
    public async Task<IReadOnlyList<PeerMessageDto>> GetMailboxAsync(Guid playerId, CancellationToken ct)
    {
        var messages = await db.PeerMessages.AsNoTracking()
            .Where(m => m.FromPlayerId == playerId || m.ToPlayerId == playerId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return [];
        }

        var playerIds = messages
            .SelectMany(m => new[] { m.FromPlayerId, m.ToPlayerId })
            .Distinct()
            .ToList();

        var usernames = await db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

        return messages.Select(m => MapMessage(m, usernames, playerId)).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid playerId, CancellationToken ct) =>
        await db.PeerMessages.AsNoTracking()
            .CountAsync(m => m.ToPlayerId == playerId && m.ReadAt == null, ct);

    public async Task<(PeerMessageDto? Message, string? Error)> SendMessageAsync(
        Guid fromPlayerId,
        Guid toPlayerId,
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

        if (fromPlayerId == toPlayerId)
        {
            return (null, "You cannot message yourself.");
        }

        if (toPlayerId == Guid.Empty)
        {
            return (null, "ONN correspondents are not available for direct miner messages.");
        }

        var recipientExists = await db.Players.AsNoTracking()
            .AnyAsync(p => p.Id == toPlayerId, ct);
        if (!recipientExists)
        {
            return (null, "Player not found.");
        }

        if (!await AreFriendsAsync(fromPlayerId, toPlayerId, ct))
        {
            return (null, "You can only message accepted friends.");
        }

        var message = new PeerMessageEntity
        {
            Id = Guid.NewGuid(),
            FromPlayerId = fromPlayerId,
            ToPlayerId = toPlayerId,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        db.PeerMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var usernames = await db.Players.AsNoTracking()
            .Where(p => p.Id == fromPlayerId || p.Id == toPlayerId)
            .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.Peer,
            usernames.GetValueOrDefault(fromPlayerId, fromPlayerId.ToString()),
            usernames.GetValueOrDefault(toPlayerId, toPlayerId.ToString()),
            message.Id,
            body);

        await messageModerationService.ScanAndFlagIfNeededAsync(
            MessageLogChannels.Peer,
            message.Id,
            fromPlayerId,
            usernames.GetValueOrDefault(fromPlayerId, "Unknown"),
            usernames.GetValueOrDefault(toPlayerId, "Unknown"),
            body,
            ct);

        return (MapMessage(message, usernames, fromPlayerId), null);
    }

    public async Task<(PeerMessageDto? Message, string? Error)> MarkReadAsync(
        Guid messageId,
        Guid playerId,
        CancellationToken ct)
    {
        var message = await db.PeerMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return (null, "Message not found.");
        }

        if (message.ToPlayerId != playerId)
        {
            return (null, "You can only mark messages sent to you as read.");
        }

        if (message.ReadAt is null)
        {
            message.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var usernames = await db.Players.AsNoTracking()
            .Where(p => p.Id == message.FromPlayerId || p.Id == message.ToPlayerId)
            .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

        return (MapMessage(message, usernames, playerId), null);
    }

    private async Task<bool> AreFriendsAsync(Guid playerA, Guid playerB, CancellationToken ct) =>
        await db.Friendships.AsNoTracking().AnyAsync(
            f => f.Status == FriendshipStatuses.Accepted
                 && ((f.PlayerId == playerA && f.FriendId == playerB)
                     || (f.PlayerId == playerB && f.FriendId == playerA)),
            ct);

    private static PeerMessageDto MapMessage(
        PeerMessageEntity message,
        IReadOnlyDictionary<Guid, string> usernames,
        Guid viewerId)
    {
        var fromUsername = usernames.GetValueOrDefault(message.FromPlayerId, "Unknown");
        var toUsername = usernames.GetValueOrDefault(message.ToPlayerId, "Unknown");
        var isSentByViewer = message.FromPlayerId == viewerId;
        var isUnread = !isSentByViewer && message.ReadAt is null;

        return new PeerMessageDto(
            message.Id,
            message.FromPlayerId,
            fromUsername,
            message.ToPlayerId,
            toUsername,
            message.Body,
            message.CreatedAt,
            message.ReadAt,
            !isUnread,
            isSentByViewer);
    }
}
