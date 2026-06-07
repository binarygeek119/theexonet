using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Foreverfall;

/// <summary>
/// Processes midnight UTC inmate intake and purges expired roster history.
/// </summary>
public sealed class ForeverfallSchedulerService(
    ForeverfallPenitentiaryService penitentiaryService,
    IOptions<ForeverfallOptions> options,
    ILogger<ForeverfallSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Foreverfall Penitentiary scheduler disabled.");
            return;
        }

        var purged = penitentiaryService.PurgeExpiredRosters();
        if (purged > 0)
        {
            logger.LogInformation("Foreverfall purged {Count} expired roster(s) on startup", purged);
        }

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

            logger.LogInformation("UTC midnight reached. Running Foreverfall Penitentiary intake.");
            await RunMidnightCycleAsync(forceRegenerate: true, stoppingToken);
        }
    }

    private async Task RunMidnightCycleAsync(bool forceRegenerate, CancellationToken cancellationToken)
    {
        var today = UtcGameClock.Today;
        try
        {
            var purged = penitentiaryService.PurgeExpiredRosters();
            if (purged > 0)
            {
                logger.LogInformation("Foreverfall purged {Count} expired roster(s)", purged);
            }

            await penitentiaryService.EnsureDailyIntakeAsync(today, forceRegenerate, cancellationToken);
            logger.LogInformation(
                "Foreverfall scheduler finished for {Date} (force={Force})",
                today,
                forceRegenerate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Foreverfall scheduler failed for UTC {UtcDate}", today);
        }
    }
}
