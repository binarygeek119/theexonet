using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public sealed class AiImageQueueService(
    AppDbContext db,
    IEnumerable<IAiImageJobHandler> handlers,
    IOptions<AiImageQueueOptions> options,
    ILogger<AiImageQueueService> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IReadOnlyDictionary<string, IAiImageJobHandler> _handlers =
        handlers.ToDictionary(handler => handler.Kind, StringComparer.Ordinal);

    public bool IsEnabled => options.Value.Enabled;

    public async Task<AiImageQueueEnqueueResult> EnqueueAsync(
        string kind,
        object payload,
        string source,
        CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new AiImageQueueEnqueueResult(0, "AI generation queue is disabled.");
        }

        if (!_handlers.ContainsKey(kind))
        {
            return new AiImageQueueEnqueueResult(0, $"Unknown AI generation job kind: {kind}");
        }

        var entry = new AiImageQueueEntity
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Payload = JsonSerializer.Serialize(payload, SerializerOptions),
            Status = AiImageQueueStatuses.Queued,
            Source = source,
            RequestedAt = DateTime.UtcNow,
        };

        db.AiImageQueue.Add(entry);
        await db.SaveChangesAsync(ct);
        return new AiImageQueueEnqueueResult(1, null);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueUniqueAsync(
        string kind,
        object payload,
        string source,
        CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new AiImageQueueEnqueueResult(0, "AI generation queue is disabled.");
        }

        if (!_handlers.ContainsKey(kind))
        {
            return new AiImageQueueEnqueueResult(0, $"Unknown AI generation job kind: {kind}");
        }

        var alreadyPending = await db.AiImageQueue.AsNoTracking()
            .AnyAsync(
                job => job.Kind == kind
                    && job.Source == source
                    && (job.Status == AiImageQueueStatuses.Queued
                        || job.Status == AiImageQueueStatuses.Processing),
                ct);

        if (alreadyPending)
        {
            return new AiImageQueueEnqueueResult(0, "Job already queued or processing.");
        }

        return await EnqueueAsync(kind, payload, source, ct);
    }

    public async Task<AiGenerationQueueWaitResult> WaitForCompletionAsync(
        string kind,
        string source,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        var pollInterval = TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var job = await db.AiImageQueue.AsNoTracking()
                .Where(entry => entry.Kind == kind && entry.Source == source)
                .OrderByDescending(entry => entry.RequestedAt)
                .FirstOrDefaultAsync(ct);

            if (job is null)
            {
                await Task.Delay(pollInterval, ct);
                continue;
            }

            if (job.Status == AiImageQueueStatuses.Completed)
            {
                return new AiGenerationQueueWaitResult(true, false, null, job.Id);
            }

            if (job.Status == AiImageQueueStatuses.Failed)
            {
                return new AiGenerationQueueWaitResult(false, true, job.Error, job.Id);
            }

            await Task.Delay(pollInterval, ct);
        }

        return new AiGenerationQueueWaitResult(false, false, "Timed out waiting for generation job.", null);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueManyAsync(
        IEnumerable<(string Kind, object Payload)> jobs,
        string source,
        CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new AiImageQueueEnqueueResult(0, "AI generation queue is disabled.");
        }

        var enqueued = 0;
        foreach (var (kind, payload) in jobs)
        {
            if (!_handlers.ContainsKey(kind))
            {
                logger.LogWarning("Skipping unknown AI generation job kind {Kind}", kind);
                continue;
            }

            db.AiImageQueue.Add(new AiImageQueueEntity
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Payload = JsonSerializer.Serialize(payload, SerializerOptions),
                Status = AiImageQueueStatuses.Queued,
                Source = source,
                RequestedAt = DateTime.UtcNow,
            });
            enqueued++;
        }

        if (enqueued > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new AiImageQueueEnqueueResult(
            enqueued,
            enqueued > 0 ? $"Queued {enqueued} generation job(s)." : null);
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var next = await db.AiImageQueue
            .Where(job => job.Status == AiImageQueueStatuses.Queued)
            .OrderBy(job => job.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (next is null)
        {
            return false;
        }

        if (!_handlers.TryGetValue(next.Kind, out var handler))
        {
            await FailAsync(next, $"No handler registered for kind {next.Kind}.", ct);
            return true;
        }

        next.Status = AiImageQueueStatuses.Processing;
        next.StartedAt = DateTime.UtcNow;
        next.Error = null;
        await db.SaveChangesAsync(ct);

        try
        {
            var (ok, error) = await handler.ExecuteAsync(next.Payload, ct);
            if (ok)
            {
                next.Status = AiImageQueueStatuses.Completed;
                next.CompletedAt = DateTime.UtcNow;
                next.Error = null;
            }
            else
            {
                await FailAsync(next, error ?? "Generation job failed.", ct);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI generation job {JobId} ({Kind}) failed.", next.Id, next.Kind);
            await FailAsync(next, ex.Message, ct);
            return true;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RecoverStaleProcessingAsync(CancellationToken ct)
    {
        var stale = await db.AiImageQueue
            .Where(job => job.Status == AiImageQueueStatuses.Processing)
            .ToListAsync(ct);

        foreach (var job in stale)
        {
            job.Status = AiImageQueueStatuses.Queued;
            job.StartedAt = null;
            job.Error = "Recovered after interrupted processing.";
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Recovered {Count} stale AI generation queue job(s).", stale.Count);
        }

        return stale.Count;
    }

    public async Task<AdminAiImageQueueStatusDto> GetStatusAsync(string? kindFilter, CancellationToken ct)
    {
        var today = UtcGameClock.Today;
        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var queuedQuery = db.AiImageQueue.AsNoTracking()
            .Where(job => job.Status == AiImageQueueStatuses.Queued);

        if (!string.IsNullOrWhiteSpace(kindFilter))
        {
            queuedQuery = ApplyKindFilter(queuedQuery, kindFilter);
        }

        var queuedCount = await queuedQuery.CountAsync(ct);
        var queuedByKind = await queuedQuery
            .GroupBy(job => job.Kind)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, ct);

        var processingQuery = db.AiImageQueue.AsNoTracking()
            .Where(job => job.Status == AiImageQueueStatuses.Processing);
        if (!string.IsNullOrWhiteSpace(kindFilter))
        {
            processingQuery = ApplyKindFilter(processingQuery, kindFilter);
        }

        var processing = await processingQuery
            .OrderBy(job => job.StartedAt)
            .FirstOrDefaultAsync(ct);

        var completedToday = await db.AiImageQueue.AsNoTracking()
            .CountAsync(
                job => job.Status == AiImageQueueStatuses.Completed
                    && job.CompletedAt != null
                    && job.CompletedAt >= todayStart,
                ct);

        var failedToday = await db.AiImageQueue.AsNoTracking()
            .CountAsync(
                job => job.Status == AiImageQueueStatuses.Failed
                    && job.CompletedAt != null
                    && job.CompletedAt >= todayStart,
                ct);

        string? description = null;
        string? currentKind = null;
        var status = queuedCount > 0 || processing is not null ? "running" : "idle";

        if (processing is not null && _handlers.TryGetValue(processing.Kind, out var handler))
        {
            currentKind = processing.Kind;
            description = handler.Describe(processing.Payload);
            if (queuedCount > 0)
            {
                description += $" ({queuedCount} queued)";
            }
        }
        else if (queuedCount > 0)
        {
            description = $"{queuedCount} generation job(s) waiting.";
        }

        return new AdminAiImageQueueStatusDto(
            status,
            description,
            currentKind,
            queuedCount,
            completedToday,
            failedToday,
            queuedByKind);
    }

    public async Task<bool> HasActiveJobsAsync(string? kindFilter, CancellationToken ct)
    {
        var query = db.AiImageQueue.AsNoTracking()
            .Where(job =>
                job.Status == AiImageQueueStatuses.Queued
                || job.Status == AiImageQueueStatuses.Processing);

        if (!string.IsNullOrWhiteSpace(kindFilter))
        {
            query = ApplyKindFilter(query, kindFilter);
        }

        return await query.AnyAsync(ct);
    }

    private static IQueryable<AiImageQueueEntity> ApplyKindFilter(
        IQueryable<AiImageQueueEntity> query,
        string kindFilter)
    {
        if (string.Equals(kindFilter, "onn_reporter", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(job =>
                job.Kind == AiImageJobKinds.OnnReporterAvatar
                || job.Kind == AiImageJobKinds.OnnReporterBackground);
        }

        var groupKinds = AiGenerationJobKinds.ResolveGroupKinds(kindFilter);
        if (groupKinds.Count > 0)
        {
            return query.Where(job => groupKinds.Contains(job.Kind));
        }

        return query.Where(job => job.Kind == kindFilter);
    }

    private async Task FailAsync(AiImageQueueEntity entry, string error, CancellationToken ct)
    {
        entry.Status = AiImageQueueStatuses.Failed;
        entry.Error = error;
        entry.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
