using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.OffworldNews;

/// <summary>
/// Generates Offworld News on API startup and refreshes the edition at each UTC midnight boundary.
/// </summary>
public sealed class OffworldNewsSchedulerService(
    OffworldNewsService offworldNewsService,
    IOptions<OffworldNewsOptions> options,
    ILiveUpdateBroadcaster liveUpdateBroadcaster,
    ILogger<OffworldNewsSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Offworld News scheduler disabled.");
            return;
        }

        await RefreshTodayAsync(forceRegenerate: false, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UtcGameClock.NextDayBoundaryUtc - DateTime.UtcNow + TimeSpan.FromSeconds(2);
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            logger.LogInformation("UTC midnight reached. Publishing new Offworld News edition.");
            await RefreshTodayAsync(forceRegenerate: true, stoppingToken);
        }
    }

    private async Task RefreshTodayAsync(bool forceRegenerate, CancellationToken cancellationToken)
    {
        var today = UtcGameClock.Today;
        try
        {
            await offworldNewsService.EnsureEditionAsync(today, forceRegenerate, cancellationToken);
            if (forceRegenerate)
            {
                LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Exonet);
            }

            logger.LogInformation(
                "Offworld News scheduler finished for {Date} (force={Force})",
                today,
                forceRegenerate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Offworld News scheduler failed for UTC {UtcDate}", today);
        }
    }
}
