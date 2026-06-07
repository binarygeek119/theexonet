using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class CompanyLogoQueueService(
    AppDbContext db,
    ICompanyLogoGenerator generator,
    ICompanyLogoStorage companyLogoStorage,
    IOptions<CompanyLogoOptions> companyLogoOptions,
    IServiceScopeFactory scopeFactory,
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
        await EnqueueMasterJobAsync(entry, ct);

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

        var createdEntries = new List<CompanyLogoQueueEntity>();
        foreach (var mine in candidates)
        {
            if (await HasActiveQueueItemAsync(mine.Id, ct))
            {
                continue;
            }

            var entry = new CompanyLogoQueueEntity
            {
                Id = Guid.NewGuid(),
                MineId = mine.Id,
                PlayerId = mine.PlayerId,
                Status = CompanyLogoQueueStatuses.Queued,
                Source = CompanyLogoQueueSources.Midnight,
                RequestedAt = DateTime.UtcNow,
            };
            db.CompanyLogoQueue.Add(entry);
            createdEntries.Add(entry);
        }

        if (createdEntries.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var entry in createdEntries)
            {
                await EnqueueMasterJobAsync(entry, ct);
            }

            logger.LogInformation(
                "Queued {Count} company logos for midnight AI generation.",
                createdEntries.Count);
        }

        return createdEntries.Count;
    }

    public async Task<(bool Ok, string? Error)> ProcessQueueEntryAsync(
        CompanyLogoQueueEntity entry,
        CancellationToken ct)
    {
        if (!IsAiEnabled)
        {
            return (false, "AI logo generation is not available on this server.");
        }

        if (entry.Status is CompanyLogoQueueStatuses.Completed or CompanyLogoQueueStatuses.Failed)
        {
            return (true, null);
        }

        entry.Status = CompanyLogoQueueStatuses.Processing;
        entry.StartedAt = DateTime.UtcNow;
        entry.Error = null;
        await db.SaveChangesAsync(ct);

        var mine = await db.Mines.FirstOrDefaultAsync(m => m.Id == entry.MineId, ct);
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == entry.PlayerId, ct);
        if (mine is null || player is null)
        {
            await FailAsync(entry, "Mine or player no longer exists.", ct);
            return (false, "Mine or player no longer exists.");
        }

        if (entry.Source == CompanyLogoQueueSources.Midnight
            && (mine.CompanyLogoIsCustom || !string.IsNullOrWhiteSpace(mine.CompanyLogoUrl)))
        {
            await CompleteSkippedAsync(entry, "Logo already present.", ct);
            return (true, null);
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
            await FailAsync(entry, error ?? "Logo generation failed.", ct);
            return (false, error ?? "Logo generation failed.");
        }

        try
        {
            await using var stream = new MemoryStream(pngBytes);
            mine.CompanyLogoUrl = await companyLogoStorage.SaveAsync(mine.Id, stream, ct);
            mine.CompanyLogoRevision++;
            mine.CompanyLogoIsCustom = false;
            entry.Status = CompanyLogoQueueStatuses.Completed;
            entry.CompletedAt = DateTime.UtcNow;
            entry.Error = null;
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Generated AI company logo for mine {MineId} ({CompanyName}).",
                mine.Id,
                mine.Name);
            return (true, null);
        }
        catch (Exception ex)
        {
            await FailAsync(entry, $"Failed to save logo: {ex.Message}", ct);
            return (false, ex.Message);
        }
    }

    private async Task EnqueueMasterJobAsync(CompanyLogoQueueEntity entry, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        await queue.EnqueueAsync(
            AiImageJobKinds.CompanyLogo,
            new CompanyLogoJobPayload(entry.Id, entry.MineId),
            entry.Source,
            ct);
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
