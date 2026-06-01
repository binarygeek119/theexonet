using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class CompanyLogoQueueService(
    AppDbContext db,
    ICompanyLogoGenerator generator,
    ICompanyLogoStorage companyLogoStorage,
    IOptions<CompanyLogoOptions> companyLogoOptions,
    ILogger<CompanyLogoQueueService> logger)
{
    public bool IsAiEnabled =>
        companyLogoOptions.Value.Enabled && generator.IsConfigured;

    public async Task<(bool Success, string Message, CompanyLogoGenerationStatusDto? Status)> EnqueueForPlayerAsync(
        Guid playerId,
        CancellationToken ct)
    {
        if (!IsAiEnabled)
        {
            return (false, "AI logo generation is not available on this server.", null);
        }

        var mine = await db.Mines.FirstOrDefaultAsync(
            m => m.PlayerId == playerId && m.Status == MineStatus.Active,
            ct);
        if (mine is null)
        {
            return (false, "You need an active mine before requesting a company logo.", null);
        }

        if (await HasActiveQueueItemAsync(mine.Id, ct))
        {
            var existing = await GetStatusForMineAsync(mine.Id, ct);
            return (false, "Your company logo is already queued for generation.", existing);
        }

        mine.CompanyLogoIsCustom = false;
        var entry = new CompanyLogoQueueEntity
        {
            Id = Guid.NewGuid(),
            MineId = mine.Id,
            PlayerId = playerId,
            Status = CompanyLogoQueueStatuses.Queued,
            Source = CompanyLogoQueueSources.User,
            RequestedAt = DateTime.UtcNow,
        };
        db.CompanyLogoQueue.Add(entry);
        await db.SaveChangesAsync(ct);

        var status = ToStatusDto(entry, "Queued for AI logo generation.");
        return (true, status.Message, status);
    }

    public async Task<int> EnqueueMissingLogosAtMidnightAsync(CancellationToken ct)
    {
        if (!IsAiEnabled)
        {
            return 0;
        }

        var candidates = await db.Mines.AsNoTracking()
            .Where(m =>
                m.Status == MineStatus.Active &&
                !m.CompanyLogoIsCustom &&
                m.CompanyLogoUrl == string.Empty)
            .Select(m => new { m.Id, m.PlayerId })
            .ToListAsync(ct);

        var enqueued = 0;
        foreach (var mine in candidates)
        {
            if (await HasActiveQueueItemAsync(mine.Id, ct))
            {
                continue;
            }

            db.CompanyLogoQueue.Add(new CompanyLogoQueueEntity
            {
                Id = Guid.NewGuid(),
                MineId = mine.Id,
                PlayerId = mine.PlayerId,
                Status = CompanyLogoQueueStatuses.Queued,
                Source = CompanyLogoQueueSources.Midnight,
                RequestedAt = DateTime.UtcNow,
            });
            enqueued++;
        }

        if (enqueued > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Queued {Count} company logos for midnight AI generation.", enqueued);
        }

        return enqueued;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!IsAiEnabled)
        {
            return false;
        }

        var next = await db.CompanyLogoQueue
            .Where(q => q.Status == CompanyLogoQueueStatuses.Queued)
            .OrderBy(q => q.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (next is null)
        {
            return false;
        }

        next.Status = CompanyLogoQueueStatuses.Processing;
        next.StartedAt = DateTime.UtcNow;
        next.Error = null;
        await db.SaveChangesAsync(ct);

        var mine = await db.Mines.FirstOrDefaultAsync(m => m.Id == next.MineId, ct);
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == next.PlayerId, ct);
        if (mine is null || player is null)
        {
            await FailAsync(next, "Mine or player no longer exists.", ct);
            return true;
        }

        if (next.Source == CompanyLogoQueueSources.Midnight
            && (mine.CompanyLogoIsCustom || !string.IsNullOrWhiteSpace(mine.CompanyLogoUrl)))
        {
            await CompleteSkippedAsync(next, "Logo already present.", ct);
            return true;
        }

        var (pngBytes, error) = await generator.GenerateAsync(
            mine.Name,
            player.Username,
            player.ProfileMood,
            player.ProfileAboutMe,
            player.ProfileInterests,
            player.ProfileMusic,
            ct);

        if (pngBytes is null)
        {
            await FailAsync(next, error ?? "Logo generation failed.", ct);
            return true;
        }

        try
        {
            await using var stream = new MemoryStream(pngBytes);
            mine.CompanyLogoUrl = await companyLogoStorage.SaveAsync(mine.Id, stream, ct);
            mine.CompanyLogoRevision++;
            mine.CompanyLogoIsCustom = false;
            next.Status = CompanyLogoQueueStatuses.Completed;
            next.CompletedAt = DateTime.UtcNow;
            next.Error = null;
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Generated AI company logo for mine {MineId} ({CompanyName}).",
                mine.Id,
                mine.Name);
        }
        catch (Exception ex)
        {
            await FailAsync(next, $"Failed to save logo: {ex.Message}", ct);
        }

        return true;
    }

    public async Task CancelPendingForMineAsync(Guid mineId, CancellationToken ct)
    {
        var pending = await db.CompanyLogoQueue
            .Where(q =>
                q.MineId == mineId &&
                (q.Status == CompanyLogoQueueStatuses.Queued ||
                 q.Status == CompanyLogoQueueStatuses.Processing))
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var item in pending)
        {
            item.Status = CompanyLogoQueueStatuses.Failed;
            item.Error = "Cancelled — manual logo uploaded.";
            item.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<CompanyLogoGenerationStatusDto> GetStatusForMineAsync(Guid mineId, CancellationToken ct)
    {
        var active = await db.CompanyLogoQueue.AsNoTracking()
            .Where(q =>
                q.MineId == mineId &&
                (q.Status == CompanyLogoQueueStatuses.Queued ||
                 q.Status == CompanyLogoQueueStatuses.Processing))
            .OrderByDescending(q => q.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (active is not null)
        {
            return ToStatusDto(active, DescribeActive(active));
        }

        var latest = await db.CompanyLogoQueue.AsNoTracking()
            .Where(q => q.MineId == mineId)
            .OrderByDescending(q => q.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            return new CompanyLogoGenerationStatusDto("none", string.Empty, null, null, null);
        }

        if (latest.Status == CompanyLogoQueueStatuses.Failed)
        {
            return ToStatusDto(latest, latest.Error ?? "Logo generation failed.");
        }

        if (latest.Status == CompanyLogoQueueStatuses.Completed)
        {
            return new CompanyLogoGenerationStatusDto("none", string.Empty, null, null, latest.CompletedAt);
        }

        return ToStatusDto(latest, DescribeActive(latest));
    }

    private async Task<bool> HasActiveQueueItemAsync(Guid mineId, CancellationToken ct) =>
        await db.CompanyLogoQueue.AsNoTracking()
            .AnyAsync(
                q =>
                    q.MineId == mineId &&
                    (q.Status == CompanyLogoQueueStatuses.Queued ||
                     q.Status == CompanyLogoQueueStatuses.Processing),
                ct);

    private async Task FailAsync(CompanyLogoQueueEntity entry, string error, CancellationToken ct)
    {
        entry.Status = CompanyLogoQueueStatuses.Failed;
        entry.Error = error;
        entry.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogWarning(
            "Company logo generation failed for mine {MineId}: {Error}",
            entry.MineId,
            error);
    }

    private async Task CompleteSkippedAsync(CompanyLogoQueueEntity entry, string reason, CancellationToken ct)
    {
        entry.Status = CompanyLogoQueueStatuses.Completed;
        entry.Error = reason;
        entry.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static CompanyLogoGenerationStatusDto ToStatusDto(
        CompanyLogoQueueEntity entry,
        string message) =>
        new(
            entry.Status,
            message,
            entry.RequestedAt,
            entry.StartedAt,
            entry.CompletedAt);

    private static string DescribeActive(CompanyLogoQueueEntity entry) =>
        entry.Status switch
        {
            CompanyLogoQueueStatuses.Queued => entry.Source == CompanyLogoQueueSources.Midnight
                ? "Queued for tonight's AI logo batch."
                : "Queued for AI logo generation.",
            CompanyLogoQueueStatuses.Processing => "Generating your company logo…",
            _ => entry.Error ?? string.Empty,
        };
}
