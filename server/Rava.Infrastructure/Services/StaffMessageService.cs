using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class StaffMessageService(AppDbContext db, ILogger<StaffMessageService> logger)
{
    public IReadOnlyList<StaffMemberDto> GetStaffMembers(
        string currentUsername,
        IReadOnlyList<StaffMemberDto> configuredStaff)
    {
        var current = currentUsername.Trim();
        return configuredStaff
            .Where(member => !string.Equals(member.Username, current, StringComparison.OrdinalIgnoreCase))
            .OrderBy(member => member.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<StaffMessageDto>> GetInboxAsync(string username, CancellationToken ct)
    {
        var normalized = username.Trim();
        var messages = await db.StaffMessages.AsNoTracking()
            .Where(m => m.ToUsername.ToLower() == normalized.ToLower())
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return messages.Select(MapMessage).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string username, CancellationToken ct)
    {
        var normalized = username.Trim();
        return await db.StaffMessages.AsNoTracking()
            .CountAsync(
                m => m.ToUsername.ToLower() == normalized.ToLower() && m.ReadAt == null,
                ct);
    }

    public async Task<(StaffMessageDto? Message, string? Error)> SendMessageAsync(
        string fromUsername,
        string toUsername,
        string body,
        IReadOnlySet<string> allowedStaffUsernames,
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

        var from = fromUsername.Trim();
        var to = toUsername.Trim();
        if (string.IsNullOrWhiteSpace(to))
        {
            return (null, "Select a recipient.");
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "You cannot message yourself.");
        }

        if (!IsAllowedStaffUsername(from, allowedStaffUsernames)
            || !IsAllowedStaffUsername(to, allowedStaffUsernames))
        {
            return (null, "Messages can only be sent between configured admin and moderator accounts.");
        }

        var message = new StaffMessageEntity
        {
            Id = Guid.NewGuid(),
            FromUsername = from,
            ToUsername = to,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        db.StaffMessages.Add(message);
        await db.SaveChangesAsync(ct);

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.StaffToStaff,
            from,
            to,
            message.Id,
            body);

        return (MapMessage(message), null);
    }

    public async Task<(StaffMessageDto? Message, string? Error)> MarkReadAsync(
        Guid messageId,
        string username,
        CancellationToken ct)
    {
        var message = await db.StaffMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            return (null, "Message not found.");
        }

        if (!string.Equals(message.ToUsername, username.Trim(), StringComparison.OrdinalIgnoreCase))
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

    private static bool IsAllowedStaffUsername(string username, IReadOnlySet<string> allowedStaffUsernames) =>
        allowedStaffUsernames.Contains(username);

    private static StaffMessageDto MapMessage(StaffMessageEntity message) =>
        new(
            message.Id,
            message.FromUsername,
            message.ToUsername,
            message.Body,
            message.CreatedAt,
            message.ReadAt,
            message.ReadAt is not null);
}
