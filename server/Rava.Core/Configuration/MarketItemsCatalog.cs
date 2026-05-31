using Rava.Core.Constants;
using Rava.Core.Enums;
using Rava.Core.Interfaces;

namespace Rava.Core.Configuration;

public sealed class MarketItemsCatalog : IMarketItemsCatalog
{
    private readonly Dictionary<OreType, decimal> _oreBasePrices;
    private readonly Dictionary<SupplyType, decimal> _supplyBasePrices;
    private readonly Dictionary<SupplyType, decimal> _supplyDailyConsumption;
    private readonly Dictionary<SupplyType, string> _supplyStockSymbols;
    private readonly Dictionary<string, decimal> _referenceCloses;

    public MarketItemsCatalog(
        Dictionary<OreType, decimal> oreBasePrices,
        Dictionary<SupplyType, decimal> supplyBasePrices,
        Dictionary<SupplyType, decimal> supplyDailyConsumption,
        Dictionary<SupplyType, string> supplyStockSymbols,
        Dictionary<string, decimal> referenceCloses)
    {
        _oreBasePrices = oreBasePrices;
        _supplyBasePrices = supplyBasePrices;
        _supplyDailyConsumption = supplyDailyConsumption;
        _supplyStockSymbols = supplyStockSymbols;
        _referenceCloses = referenceCloses;
    }

    public decimal GetOreBasePrice(OreType oreType) =>
        _oreBasePrices.TryGetValue(oreType, out var price)
            ? price
            : throw new KeyNotFoundException($"Ore base price not configured for {oreType}.");

    public decimal GetSupplyBasePrice(SupplyType supplyType) =>
        _supplyBasePrices.TryGetValue(supplyType, out var price)
            ? price
            : throw new KeyNotFoundException($"Supply base price not configured for {supplyType}.");

    public decimal GetSupplyDailyConsumption(SupplyType supplyType) =>
        _supplyDailyConsumption.TryGetValue(supplyType, out var amount)
            ? amount
            : throw new KeyNotFoundException($"Supply daily consumption not configured for {supplyType}.");

    public string GetSupplyStockSymbol(SupplyType supplyType) =>
        _supplyStockSymbols.TryGetValue(supplyType, out var symbol) && !string.IsNullOrWhiteSpace(symbol)
            ? symbol.Trim().ToUpperInvariant()
            : supplyType switch
            {
                SupplyType.DrillBits => "CAT",
                SupplyType.FuelCells => "XOM",
                SupplyType.LifeSupport => "JNJ",
                SupplyType.CommModules => "QCOM",
                _ => "SPY"
            };

    public decimal GetReferenceClose(string stockSymbol)
    {
        var normalized = stockSymbol.Trim().ToUpperInvariant();
        return _referenceCloses.TryGetValue(normalized, out var price) ? price : 0m;
    }

    public static MarketItemsCatalog CreateDefault()
    {
        var orePrices = GameBalance.BaseOrePrices.ToDictionary(entry => entry.Key, entry => entry.Value);
        var supplyPrices = GameBalance.BaseSupplyPrices.ToDictionary(entry => entry.Key, entry => entry.Value);
        var consumption = GameBalance.SupplyConsumptionPerDay.ToDictionary(entry => entry.Key, entry => entry.Value);
        var symbols = new Dictionary<SupplyType, string>
        {
            [SupplyType.DrillBits] = "CAT",
            [SupplyType.FuelCells] = "XOM",
            [SupplyType.LifeSupport] = "JNJ",
            [SupplyType.CommModules] = "QCOM"
        };
        var referenceCloses = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["CAT"] = 350m,
            ["XOM"] = 110m,
            ["JNJ"] = 155m,
            ["QCOM"] = 170m
        };

        return new MarketItemsCatalog(orePrices, supplyPrices, consumption, symbols, referenceCloses);
    }
}
