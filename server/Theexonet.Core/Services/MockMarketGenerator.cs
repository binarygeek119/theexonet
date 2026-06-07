using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public class MockMarketGenerator(IMarketItemsCatalog marketItems) : IMarketDataProvider
{
    public Task<DailyMarketSnapshot> GetDailyPricesAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken cancellationToken = default)
    {
        var rng = new Random(42 + gameDay * 7919);
        var sectorMomentum = (decimal)(rng.NextDouble() * 0.06 - 0.03);

        var prices = new List<MarketPriceEntry>();
        var supplyTypes = Enum.GetValues<SupplyType>();

        for (var i = 0; i < supplyTypes.Length; i++)
        {
            var supplyType = supplyTypes[i];
            var basePrice = marketItems.GetSupplyBasePrice(supplyType);
            var typeRng = new Random(42 + gameDay * 1009 + i * 17);
            var dailyChange = sectorMomentum + (decimal)(typeRng.NextDouble() * 0.08 - 0.04);

            var cumulativeMultiplier = 1m;
            for (var day = 1; day <= gameDay; day++)
            {
                var dayRng = new Random(42 + day * 1009 + i * 17);
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
            Date = utcDate,
            Source = "mock",
            Prices = prices
        });
    }
}
