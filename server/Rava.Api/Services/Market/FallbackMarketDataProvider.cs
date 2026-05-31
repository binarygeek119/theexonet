using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Core.Services;

namespace Rava.Api.Services.Market;

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
}
