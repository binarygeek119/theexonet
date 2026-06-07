using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.AiImageQueue;

/// <summary>
/// Recovers stale queue jobs and migrates orphaned company-logo requests on startup.
/// </summary>
public sealed class AiImageQueueRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<AiImageQueueRecoveryService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();

        var recovered = await queue.RecoverStaleProcessingAsync(cancellationToken);
        var migrated = await MigrateCompanyLogoQueueAsync(db, queue, cancellationToken);

        if (recovered > 0 || migrated > 0)
        {
            logger.LogInformation(
                "AI image queue recovery finished: {Recovered} stale job(s), {Migrated} company logo job(s) migrated.",
                recovered,
                migrated);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<int> MigrateCompanyLogoQueueAsync(
        AppDbContext db,
        AiImageQueueService queue,
        CancellationToken ct)
    {
        var orphaned = await db.CompanyLogoQueue
            .Where(entry => entry.Status == CompanyLogoQueueStatuses.Queued)
            .OrderBy(entry => entry.RequestedAt)
            .ToListAsync(ct);

        if (orphaned.Count == 0)
        {
            return 0;
        }

        var existingLogoJobs = await db.AiImageQueue.AsNoTracking()
            .Where(job =>
                job.Kind == AiImageJobKinds.CompanyLogo
                && (job.Status == AiImageQueueStatuses.Queued
                    || job.Status == AiImageQueueStatuses.Processing))
            .Select(job => job.Payload)
            .ToListAsync(ct);

        var existingIds = existingLogoJobs
            .Select(ParseCompanyLogoQueueEntityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var migrated = 0;
        foreach (var entry in orphaned)
        {
            if (existingIds.Contains(entry.Id))
            {
                continue;
            }

            await queue.EnqueueAsync(
                AiImageJobKinds.CompanyLogo,
                new CompanyLogoJobPayload(entry.Id, entry.MineId),
                $"recovery:{entry.Source}",
                ct);
            migrated++;
        }

        return migrated;
    }

    private static Guid? ParseCompanyLogoQueueEntityId(string payloadJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CompanyLogoJobPayload>(
                payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return payload?.QueueEntityId;
        }
        catch
        {
            return null;
        }
    }
}
