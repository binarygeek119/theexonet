using Microsoft.EntityFrameworkCore;
using Rava.Core.Dtos;
using Rava.Core.Services;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class ReporterFriendshipService(AppDbContext db)
{
    public async Task<IReadOnlyList<FriendSummaryDto>> GetFriendSummariesAsync(Guid playerId, CancellationToken ct)
    {
        var links = await db.ReporterFriendships.AsNoTracking()
            .Where(f => f.PlayerId == playerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var summaries = new List<FriendSummaryDto>();
        foreach (var link in links)
        {
            var reporter = OffworldNewsReporterCatalog.TryGetBySlug(link.ReporterSlug);
            if (reporter is null)
            {
                continue;
            }

            summaries.Add(new FriendSummaryDto(
                link.Id,
                Guid.Empty,
                reporter.DisplayName,
                OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug),
                reporter.Personality,
                "accepted",
                link.CreatedAt,
                IsReporter: true,
                ReporterSlug: reporter.Slug));
        }

        return summaries;
    }

    public async Task<(bool Success, string Message)> AddFriendAsync(Guid playerId, string reporterSlug, CancellationToken ct)
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug(reporterSlug);
        if (reporter is null)
        {
            return (false, "Reporter not found.");
        }

        var exists = await db.ReporterFriendships.AsNoTracking()
            .AnyAsync(f => f.PlayerId == playerId && f.ReporterSlug == reporter.Slug, ct);
        if (exists)
        {
            return (false, $"You are already friends with {reporter.DisplayName}.");
        }

        db.ReporterFriendships.Add(new ReporterFriendshipEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            ReporterSlug = reporter.Slug,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return (true, $"{reporter.DisplayName} is now on your friends list.");
    }

    public async Task<(bool Success, string Message)> RemoveFriendAsync(Guid playerId, Guid friendshipId, CancellationToken ct)
    {
        var link = await db.ReporterFriendships.FirstOrDefaultAsync(
            f => f.Id == friendshipId && f.PlayerId == playerId,
            ct);
        if (link is null)
        {
            return (false, "Friendship not found.");
        }

        var reporter = OffworldNewsReporterCatalog.TryGetBySlug(link.ReporterSlug);
        db.ReporterFriendships.Remove(link);
        await db.SaveChangesAsync(ct);

        var name = reporter?.DisplayName ?? "Reporter";
        return (true, $"Removed {name} from your friends.");
    }

    public async Task<(string Status, Guid? FriendshipId)> GetFriendshipStatusAsync(
        Guid viewerId,
        string reporterSlug,
        CancellationToken ct)
    {
        var link = await db.ReporterFriendships.AsNoTracking()
            .FirstOrDefaultAsync(f => f.PlayerId == viewerId && f.ReporterSlug == reporterSlug, ct);

        return link is null ? ("none", null) : ("accepted", link.Id);
    }

    public async Task<int> MigrateReporterSlugAsync(string oldSlug, string newSlug, CancellationToken ct)
    {
        var links = await db.ReporterFriendships
            .Where(f => f.ReporterSlug == oldSlug)
            .ToListAsync(ct);

        if (links.Count == 0)
        {
            return 0;
        }

        foreach (var link in links)
        {
            link.ReporterSlug = newSlug;
        }

        await db.SaveChangesAsync(ct);
        return links.Count;
    }
}
