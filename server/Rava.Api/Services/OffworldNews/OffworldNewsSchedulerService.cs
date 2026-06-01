using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

/// <summary>
/// Generates Offworld News on API startup and refreshes the edition at each UTC midnight boundary.
/// </summary>
public sealed class OffworldNewsSchedulerService(
    OffworldNewsService offworldNewsService,
    IOptions<OffworldNewsOptions> options,
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
