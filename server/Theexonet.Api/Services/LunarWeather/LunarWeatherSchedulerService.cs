using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.LunarWeather;

/// <summary>
/// Publishes a new Lunar Weather bulletin at each UTC midnight boundary.
/// </summary>
public sealed class LunarWeatherSchedulerService(
    LunarWeatherService lunarWeatherService,
    IOptions<LunarWeatherOptions> options,
    ILiveUpdateBroadcaster liveUpdateBroadcaster,
    ILogger<LunarWeatherSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Lunar Weather scheduler disabled.");
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

            logger.LogInformation("UTC midnight reached. Publishing new Lunar Weather bulletin.");
            await RefreshTodayAsync(forceRegenerate: true, stoppingToken);
        }
    }

    private async Task RefreshTodayAsync(bool forceRegenerate, CancellationToken cancellationToken)
    {
        var today = UtcGameClock.Today;
        try
        {
            await lunarWeatherService.EnsureBulletinAsync(today, forceRegenerate, cancellationToken);
            if (forceRegenerate)
            {
                LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Exonet);
            }

            logger.LogInformation(
                "Lunar Weather scheduler finished for {Date} (force={Force})",
                today,
                forceRegenerate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lunar Weather scheduler failed for UTC {UtcDate}", today);
        }
    }
}
