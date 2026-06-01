using Rava.Core.Dtos;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsReporterPortraitJobService(
    OffworldNewsService offworldNewsService,
    ILogger<OffworldNewsReporterPortraitJobService> logger)
{
    private readonly object _gate = new();
    private JobSnapshot _snapshot = JobSnapshot.Idle();

    public (bool Started, string? Error) TryStart(IReadOnlyList<string>? slugs)
    {
        lock (_gate)
        {
            if (_snapshot.Status is PortraitJobStatus.Running)
            {
                return (false, "Portrait regeneration is already in progress.");
            }

            _snapshot = JobSnapshot.Running(slugs);
        }

        _ = Task.Run(() => RunAsync(slugs));
        return (true, null);
    }

    public AdminOffworldNewsReporterPortraitJobDto GetStatus()
    {
        lock (_gate)
        {
            return _snapshot.ToDto();
        }
    }

    private async Task RunAsync(IReadOnlyList<string>? slugs)
    {
        try
        {
            var (summary, error) = await offworldNewsService.RegenerateReporterPortraitsAsync(slugs);
            lock (_gate)
            {
                if (summary is null)
                {
                    _snapshot = JobSnapshot.Failed(error ?? "Portrait regeneration failed.", _snapshot.StartedUtc);
                    return;
                }

                _snapshot = JobSnapshot.Completed(summary, error, _snapshot.StartedUtc);
            }

            logger.LogInformation(
                "Reporter portrait job finished: {Succeeded}/{Attempted} images for {ReporterCount} reporters.",
                summary.Succeeded,
                summary.Attempted,
                summary.ReporterCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reporter portrait regeneration job failed.");
            lock (_gate)
            {
                _snapshot = JobSnapshot.Failed(ex.Message, _snapshot.StartedUtc);
            }
        }
    }

    private enum PortraitJobStatus
    {
        Idle,
        Running,
        Completed,
        Failed,
    }

    private sealed record JobSnapshot(
        PortraitJobStatus Status,
        DateTime? StartedUtc,
        DateTime? CompletedUtc,
        int ReporterCount,
        int ImageAttempts,
        int ImagesSaved,
        string? Message,
        string? ImageGenerationError)
    {
        public static JobSnapshot Idle() =>
            new(PortraitJobStatus.Idle, null, null, 0, 0, 0, null, null);

        public static JobSnapshot Running(IReadOnlyList<string>? slugs) =>
            new(
                PortraitJobStatus.Running,
                DateTime.UtcNow,
                null,
                0,
                0,
                0,
                slugs is { Count: 1 }
                    ? $"Regenerating portraits for {slugs[0]}…"
                    : "Regenerating reporter portraits…",
                null);

        public static JobSnapshot Completed(
            OffworldNewsReporterPortraitGenerationSummary summary,
            string? error,
            DateTime? startedUtc) =>
            new(
                PortraitJobStatus.Completed,
                startedUtc,
                DateTime.UtcNow,
                summary.ReporterCount,
                summary.Attempted,
                summary.Succeeded,
                summary.Describe(),
                error ?? summary.Error);

        public static JobSnapshot Failed(string message, DateTime? startedUtc) =>
            new(PortraitJobStatus.Failed, startedUtc, DateTime.UtcNow, 0, 0, 0, message, null);

        public AdminOffworldNewsReporterPortraitJobDto ToDto() =>
            new(
                Status.ToString().ToLowerInvariant(),
                Message,
                ReporterCount,
                ImageAttempts,
                ImagesSaved,
                ImageGenerationError,
                StartedUtc,
                CompletedUtc);
    }
}
