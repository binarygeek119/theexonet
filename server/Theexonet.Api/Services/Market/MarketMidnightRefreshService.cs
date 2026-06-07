using Microsoft.Extensions.Caching.Memory;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Market;

public class MarketMidnightRefreshService(
    IMemoryCache cache,
    IMarketDataProvider marketProvider,
    ILogger<MarketMidnightRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PrefetchCurrentDayAsync(stoppingToken);

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

            if (cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }

            logger.LogInformation("UTC midnight reached. Refreshing live market prices.");
            await PrefetchCurrentDayAsync(stoppingToken);
        }
    }

    private async Task PrefetchCurrentDayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await marketProvider.GetDailyPricesAsync(0, UtcGameClock.Today, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Market prefetch failed for UTC {UtcDate}", UtcGameClock.Today);
        }
    }
}
