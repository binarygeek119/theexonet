using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class PlayerToStaffMessageService(
    AppDbContext db,
    IOptionsMonitor<AdminOptions> adminOptions,
    IOptionsMonitor<ModeratorOptions> moderatorOptions,
    ILogger<PlayerToStaffMessageService> logger,
    MessageModerationService messageModerationService)
{
    public IReadOnlyList<StaffContactDto> GetStaffContacts()
    {
        var members = new Dictionary<string, StaffContactDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var username in adminOptions.CurrentValue.Usernames ?? [])
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var name = username.Trim();
            members[name] = new StaffContactDto(
                name,
                true,
                members.TryGetValue(name, out var existing) && existing.IsModerator);
        }

        foreach (var username in moderatorOptions.CurrentValue.Usernames ?? [])
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var name = username.Trim();
            if (members.TryGetValue(name, out var existing))
            {
                members[name] = existing with { IsModerator = true };
            }
            else
            {
                members[name] = new StaffContactDto(name, false, true);
            }
        }

        return members.Values.OrderBy(member => member.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<PlayerToStaffMessageDto>> GetSentByPlayerAsync(Guid playerId, CancellationToken ct)
    {
        var messages = await db.PlayerToStaffMessages.AsNoTracking()
            .Where(m => m.PlayerId == playerId && m.HiddenForPlayerAt == null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return messages.Select(MapSentMessage).ToList();
    }

    public async Task<IReadOnlyList<PlayerToStaffInboxDto>> GetInboxForStaffAsync(string staffUsername, CancellationToken ct)
    {
        var normalized = staffUsername.Trim();
        var messages = await db.PlayerToStaffMessages.AsNoTracking()
            .Where(m =>
                m.ToStaffUsername.ToLower() == normalized.ToLower()
                && m.HiddenForStaffAt == null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            return [];
        }

        var playerIds = messages.Select(m => m.PlayerId).Distinct().ToList();
        var players = await db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

        return messages
            .Select(m => MapInboxMessage(m, players.GetValueOrDefault(m.PlayerId, "Unknown")))
            .ToList();
    }

    public async Task<int> GetUnreadCountForStaffAsync(string staffUsername, CancellationToken ct)
    {
        var normalized = staffUsername.Trim();
        return await db.PlayerToStaffMessages.AsNoTracking()
            .CountAsync(
                m => m.ToStaffUsername.ToLower() == normalized.ToLower()
                     && m.ReadAt == null
                     && m.HiddenForStaffAt == null,
                ct);
    }

    public async Task<(PlayerToStaffMessageDto? Message, string? Error)> SendFromPlayerAsync(
        Guid playerId,
        string toStaffUsername,
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

        var to = toStaffUsername.Trim();
        if (string.IsNullOrWhiteSpace(to))
        {
            return (null, "Select a staff member.");
        }

        if (!IsAllowedStaffUsername(to))
        {
            return (null, "Messages can only be sent to configured admin or moderator accounts.");
        }

        var playerExists = await db.Players.AsNoTracking().AnyAsync(p => p.Id == playerId, ct);
        if (!playerExists)
        {
            return (null, "Player not found.");
        }

        var message = new PlayerToStaffMessageEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            ToStaffUsername = to,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        db.PlayerToStaffMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var playerUsername = await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct) ?? playerId.ToString();

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.PlayerToStaff,
            playerUsername,
            to,
            message.Id,
            body);

        await messageModerationService.ScanAndFlagIfNeededAsync(
            MessageLogChannels.PlayerToStaff,
            message.Id,
            playerId,
            playerUsername,
            to,
            body,
            ct);

        return (MapSentMessage(message), null);
    }

    public async Task<(PlayerToStaffInboxDto? Message, string? Error)> MarkReadAsync(
        Guid messageId,
        string staffUsername,
        CancellationToken ct)
    {
        var message = await db.PlayerToStaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return (null, "Message not found.");
        }

        if (!string.Equals(message.ToStaffUsername, staffUsername.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (null, "You can only mark messages sent to you as read.");
        }

        if (message.HiddenForStaffAt is not null)
        {
            return (null, "Message not found.");
        }

        if (message.ReadAt is null)
        {
            message.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var username = await db.Players.AsNoTracking()
            .Where(p => p.Id == message.PlayerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        return (MapInboxMessage(message, username), null);
    }

    public async Task<string?> DeleteByPlayerAsync(Guid messageId, Guid playerId, CancellationToken ct)
    {
        var message = await db.PlayerToStaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return "Message not found.";
        }

        if (message.PlayerId != playerId)
        {
            return "You can only delete messages you sent.";
        }

        if (message.HiddenForPlayerAt is not null)
        {
            return null;
        }

        message.HiddenForPlayerAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> DeleteByStaffAsync(Guid messageId, string staffUsername, CancellationToken ct)
    {
        var message = await db.PlayerToStaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return "Message not found.";
        }

        if (!string.Equals(message.ToStaffUsername, staffUsername.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "You can only delete messages sent to you.";
        }

        if (message.HiddenForStaffAt is not null)
        {
            return null;
        }

        message.HiddenForStaffAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    private bool IsAllowedStaffUsername(string username) =>
        GetStaffContacts().Any(member => string.Equals(member.Username, username, StringComparison.OrdinalIgnoreCase));

    private static PlayerToStaffMessageDto MapSentMessage(PlayerToStaffMessageEntity message) =>
        new(
            message.Id,
            message.ToStaffUsername,
            message.Body,
            message.CreatedAt,
            message.ReadAt,
            message.ReadAt is not null);

    private static PlayerToStaffInboxDto MapInboxMessage(PlayerToStaffMessageEntity message, string playerUsername) =>
        new(
            message.Id,
            message.PlayerId,
            playerUsername,
            message.Body,
            message.CreatedAt,
            message.ReadAt,
            message.ReadAt is not null);
}
