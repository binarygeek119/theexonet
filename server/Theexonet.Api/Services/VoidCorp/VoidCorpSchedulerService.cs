using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.VoidCorp;

/// <summary>
/// Generates missing VoidCorp product images after startup and at UTC midnight.
/// </summary>
public sealed class VoidCorpSchedulerService(
    VoidCorpMissingImageBackfillService backfillService,
    IOptions<VoidCorpOptions> options,
    ILiveUpdateBroadcaster liveUpdateBroadcaster,
    ILogger<VoidCorpSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("VoidCorp scheduler disabled.");
            return;
        }

        await RunCycleAsync("startup", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UtcGameClock.NextDayBoundaryUtc - DateTime.UtcNow + TimeSpan.FromSeconds(3);
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

            await RunCycleAsync("midnight", stoppingToken);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken cancellationToken)
    {
        if (!backfillService.IsConfigured)
        {
            logger.LogDebug("VoidCorp scheduler ({Trigger}) skipped: OpenAI not configured.", trigger);
            return;
        }

        await backfillService.RunAsync(trigger, cancellationToken);
        if (trigger == "midnight")
        {
            LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Exonet);
        }
    }
}
