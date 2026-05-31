using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Core.Services;

namespace Rava.Api.Services.Market;

public class YahooFinanceMarketDataProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    IMarketItemsCatalog marketItems,
    ILogger<YahooFinanceMarketDataProvider> logger) : IMarketDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<DailyMarketSnapshot> GetDailyPricesAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"market:yahoo:{utcDate:yyyy-MM-dd}";
        return cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpiration = UtcGameClock.NextDayBoundaryUtc;
            return FetchSnapshotAsync(gameDay, utcDate, cancellationToken);
        })!;
    }

    private async Task<DailyMarketSnapshot> FetchSnapshotAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken cancellationToken)
    {
        var prices = new List<MarketPriceEntry>();

        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            var symbol = marketItems.GetSupplyStockSymbol(supplyType);
            var quote = await FetchQuoteAsync(symbol, utcDate, cancellationToken);
            var basePrice = marketItems.GetSupplyBasePrice(supplyType);
            var referenceClose = marketItems.GetReferenceClose(symbol);
            if (referenceClose <= 0)
            {
                referenceClose = quote.LatestClose;
            }

            var scaledPrice = Math.Round(basePrice * (quote.LatestClose / referenceClose), 2);
            var minimumPrice = Math.Round(basePrice * 0.4m, 2);

            prices.Add(new MarketPriceEntry
            {
                SupplyType = supplyType,
                Price = Math.Max(scaledPrice, minimumPrice),
                ChangePct = Math.Round(quote.ChangePct, 2)
            });
        }

        logger.LogInformation(
            "Loaded live US market prices for UTC {UtcDate} ({SymbolCount} symbols)",
            utcDate,
            prices.Count);

        return new DailyMarketSnapshot
        {
            GameDay = gameDay,
            Date = utcDate,
            Source = "yahoo-us",
            Prices = prices
        };
    }

    private async Task<SymbolQuote> FetchQuoteAsync(
        string symbol,
        DateOnly utcDate,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=3mo";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "RavaGame/1.0");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<YahooChartResponse>(stream, JsonOptions, cancellationToken);
        var closes = payload?.Chart?.Result?.FirstOrDefault()?.Indicators?.Quote?.FirstOrDefault()?.Close;
        var timestamps = payload?.Chart?.Result?.FirstOrDefault()?.Timestamp;

        if (closes is null || timestamps is null || closes.Count == 0)
        {
            throw new InvalidOperationException($"No market data returned for {symbol}.");
        }

        var bars = new List<(DateOnly Date, decimal Close)>();
        for (var i = 0; i < Math.Min(closes.Count, timestamps.Count); i++)
        {
            var close = closes[i];
            if (close is null or <= 0)
            {
                continue;
            }

            var date = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime);
            bars.Add((date, (decimal)close.Value));
        }

        if (bars.Count == 0)
        {
            throw new InvalidOperationException($"No valid closing prices for {symbol}.");
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));
        var currentIndex = bars.FindLastIndex(bar => bar.Date <= utcDate);
        if (currentIndex < 0)
        {
            currentIndex = bars.Count - 1;
        }

        var current = bars[currentIndex];
        var previous = currentIndex > 0 ? bars[currentIndex - 1] : current;
        var changePct = previous.Close <= 0
            ? 0m
            : (current.Close - previous.Close) / previous.Close * 100m;

        return new SymbolQuote(current.Close, changePct);
    }

    private sealed record SymbolQuote(decimal LatestClose, decimal ChangePct);

    private sealed class YahooChartResponse
    {
        public YahooChart? Chart { get; set; }
    }

    private sealed class YahooChart
    {
        public List<YahooChartResult>? Result { get; set; }
    }

    private sealed class YahooChartResult
    {
        public List<long>? Timestamp { get; set; }
        public YahooIndicators? Indicators { get; set; }
    }

    private sealed class YahooIndicators
    {
        public List<YahooQuote>? Quote { get; set; }
    }

    private sealed class YahooQuote
    {
        public List<double?>? Close { get; set; }
    }
}
