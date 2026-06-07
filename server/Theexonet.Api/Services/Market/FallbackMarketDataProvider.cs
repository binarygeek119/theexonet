using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.Market;

public class FallbackMarketDataProvider(
    YahooFinanceMarketDataProvider liveProvider,
    MockMarketGenerator mockProvider,
    IOptions<MarketOptions> options,
    ILogger<FallbackMarketDataProvider> logger) : IMarketDataProvider
{
    public async Task<DailyMarketSnapshot> GetDailyPricesAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseLiveData)
        {
            return await mockProvider.GetDailyPricesAsync(gameDay, utcDate, cancellationToken);
        }

        try
        {
            return await liveProvider.GetDailyPricesAsync(gameDay, utcDate, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Live US market fetch failed for UTC {UtcDate}. Falling back to mock prices.",
                utcDate);
            var fallback = await mockProvider.GetDailyPricesAsync(gameDay, utcDate, cancellationToken);
            fallback.Source = "mock-fallback";
            return fallback;
        }
    }

    /// <summary>Force-fetch today's prices on API startup and at UTC midnight.</summary>
    public Task<DailyMarketSnapshot> PrefetchTodayAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseLiveData)
        {
            return mockProvider.GetDailyPricesAsync(0, UtcGameClock.Today, cancellationToken);
        }

        return PrefetchLiveTodayAsync(cancellationToken);
    }

    private async Task<DailyMarketSnapshot> PrefetchLiveTodayAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await liveProvider.PrefetchTodayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Live US market prefetch failed for UTC {UtcDate}. Falling back to mock prices.",
                UtcGameClock.Today);
            var fallback = await mockProvider.GetDailyPricesAsync(0, UtcGameClock.Today, cancellationToken);
            fallback.Source = "mock-fallback";
            return fallback;
        }
    }
}
