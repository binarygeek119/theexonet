using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Market;

/// <summary>
/// Prefetches market prices on API startup and refreshes them at each UTC midnight boundary.
/// </summary>
public class MarketMidnightRefreshService(
    IMemoryCache cache,
    FallbackMarketDataProvider marketProvider,
    IOptions<MarketOptions> options,
    ILogger<MarketMidnightRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync("startup", stoppingToken);

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

            await RefreshAsync("midnight", stoppingToken);
        }
    }

    private async Task RefreshAsync(string reason, CancellationToken cancellationToken)
    {
        var today = UtcGameClock.Today;

        try
        {
            if (options.Value.UseLiveData)
            {
                if (reason == "startup")
                {
                    logger.LogInformation(
                        "API startup. Prefetching live US market prices (Yahoo Finance) for UTC {UtcDate}.",
                        today);
                }
                else
                {
                    if (cache is MemoryCache memoryCache)
                    {
                        memoryCache.Compact(1.0);
                    }

                    logger.LogInformation(
                        "UTC midnight reached. Refreshing live US market prices (Yahoo Finance) for UTC {UtcDate}.",
                        today);
                }

                await marketProvider.PrefetchTodayAsync(cancellationToken);
                return;
            }

            if (reason == "startup")
            {
                logger.LogInformation("Live market prefetch skipped (UseLiveData=false).");
            }

            await marketProvider.PrefetchTodayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Market prefetch failed for UTC {UtcDate} ({Reason})", today, reason);
        }
    }
}
