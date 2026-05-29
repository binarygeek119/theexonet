using Rava.Core.Constants;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;

namespace Rava.Core.Services;

public class MockMarketGenerator : IMarketDataProvider
{
    public Task<DailyMarketSnapshot> GetDailyPricesAsync(int gameDay, int marketSeed, CancellationToken cancellationToken = default)
    {
        var rng = new Random(marketSeed + gameDay * 7919);
        var sectorMomentum = (decimal)(rng.NextDouble() * 0.06 - 0.03);

        var prices = new List<MarketPriceEntry>();
        var supplyTypes = Enum.GetValues<SupplyType>();

        for (var i = 0; i < supplyTypes.Length; i++)
        {
            var supplyType = supplyTypes[i];
            var basePrice = GameBalance.BaseSupplyPrices[supplyType];
            var typeRng = new Random(marketSeed + gameDay * 1009 + i * 17);
            var dailyChange = sectorMomentum + (decimal)(typeRng.NextDouble() * 0.08 - 0.04);

            var cumulativeMultiplier = 1m;
            for (var day = 1; day <= gameDay; day++)
            {
                var dayRng = new Random(marketSeed + day * 1009 + i * 17);
                var dayChange = sectorMomentum + (decimal)(dayRng.NextDouble() * 0.08 - 0.04);
                cumulativeMultiplier *= 1m + dayChange;
            }

            var price = Math.Round(basePrice * cumulativeMultiplier, 2);
            prices.Add(new MarketPriceEntry
            {
                SupplyType = supplyType,
                Price = Math.Max(price, basePrice * 0.4m),
                ChangePct = Math.Round(dailyChange * 100m, 2)
            });
        }

        return Task.FromResult(new DailyMarketSnapshot
        {
            GameDay = gameDay,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "mock",
            Prices = prices
        });
    }
}
