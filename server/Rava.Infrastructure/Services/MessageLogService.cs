using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;

namespace Rava.Infrastructure.Services;

public class MessageLogService(AppDbContext db)
{
    public async Task<MessageLogResponse> GetLogAsync(
        string? search,
        string? channel,
        int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);
        var perSource = Math.Max(limit, 50);
        var normalizedChannel = channel?.Trim();
        var entries = new List<MessageLogEntryDto>();

        if (IncludeChannel(normalizedChannel, MessageLogChannels.StaffToStaff))
        {
            var staffMessages = await db.StaffMessages.AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .Take(perSource)
                .ToListAsync(ct);

            entries.AddRange(staffMessages.Select(m => new MessageLogEntryDto(
                m.Id,
                MessageLogChannels.StaffToStaff,
                m.FromUsername,
                m.ToUsername,
                m.Body,
                m.CreatedAt,
                m.ReadAt,
                m.ReadAt is not null)));
        }

        if (IncludeChannel(normalizedChannel, MessageLogChannels.StaffToPlayer))
        {
            var playerMessages = await db.PlayerMessages.AsNoTracking()
                .Join(
                    db.Players.AsNoTracking(),
                    message => message.PlayerId,
                    player => player.Id,
                    (message, player) => new { message, player.Username })
                .OrderByDescending(row => row.message.CreatedAt)
                .Take(perSource)
                .ToListAsync(ct);

            entries.AddRange(playerMessages.Select(row => new MessageLogEntryDto(
                row.message.Id,
                MessageLogChannels.StaffToPlayer,
                row.message.FromStaffUsername,
                row.Username,
                row.message.Body,
                row.message.CreatedAt,
                row.message.ReadAt,
                row.message.ReadAt is not null)));
        }

        if (IncludeChannel(normalizedChannel, MessageLogChannels.PlayerToStaff))
        {
            var staffInboxMessages = await db.PlayerToStaffMessages.AsNoTracking()
                .Join(
                    db.Players.AsNoTracking(),
                    message => message.PlayerId,
                    player => player.Id,
                    (message, player) => new { message, player.Username })
                .OrderByDescending(row => row.message.CreatedAt)
                .Take(perSource)
                .ToListAsync(ct);

            entries.AddRange(staffInboxMessages.Select(row => new MessageLogEntryDto(
                row.message.Id,
                MessageLogChannels.PlayerToStaff,
                row.Username,
                row.message.ToStaffUsername,
                row.message.Body,
                row.message.CreatedAt,
                row.message.ReadAt,
                row.message.ReadAt is not null)));
        }

        if (IncludeChannel(normalizedChannel, MessageLogChannels.Peer))
        {
            var peerMessages = await db.PeerMessages.AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .Take(perSource)
                .ToListAsync(ct);

            if (peerMessages.Count > 0)
            {
                var playerIds = peerMessages
                    .SelectMany(m => new[] { m.FromPlayerId, m.ToPlayerId })
                    .Distinct()
                    .ToList();

                var usernames = await db.Players.AsNoTracking()
                    .Where(p => playerIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Username, ct);

                entries.AddRange(peerMessages.Select(m => new MessageLogEntryDto(
                    m.Id,
                    MessageLogChannels.Peer,
                    usernames.GetValueOrDefault(m.FromPlayerId, "Unknown"),
                    usernames.GetValueOrDefault(m.ToPlayerId, "Unknown"),
                    m.Body,
                    m.CreatedAt,
                    m.ReadAt,
                    m.ReadAt is not null)));
            }
        }

        if (IncludeChannel(normalizedChannel, MessageLogChannels.BanAppeal))
        {
            var appeals = await db.BanAppeals.AsNoTracking()
                .Join(
                    db.Players.AsNoTracking(),
                    appeal => appeal.PlayerId,
                    player => player.Id,
                    (appeal, player) => new { appeal, player.Username })
                .OrderByDescending(row => row.appeal.CreatedAt)
                .Take(perSource)
                .ToListAsync(ct);

            entries.AddRange(appeals.Select(row => new MessageLogEntryDto(
                row.appeal.Id,
                MessageLogChannels.BanAppeal,
                row.Username,
                "admins",
                row.appeal.Message,
                row.appeal.CreatedAt,
                row.appeal.ReviewedAt,
                row.appeal.ReviewedAt is not null)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            entries = entries
                .Where(entry =>
                    entry.FromLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || entry.ToLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || entry.Body.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var results = entries
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(limit)
            .ToList();

        return new MessageLogResponse(results);
    }

    private static bool IncludeChannel(string? filter, string channel) =>
        string.IsNullOrWhiteSpace(filter)
        || string.Equals(filter, channel, StringComparison.OrdinalIgnoreCase);
}
