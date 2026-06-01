using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Enums;
using Rava.Core.Interfaces;

namespace Rava.Infrastructure.Services;

public class MarketItemsProvider(
    IWebHostEnvironment environment,
    IOptionsMonitor<MarketOptions> options) : IMarketItemsCatalog
{
    private MarketItemsCatalog _catalog = MarketItemsCatalog.CreateDefault();
    private DateTime _cachedWriteUtc = DateTime.MinValue;

    public decimal GetOreBasePrice(OreType oreType) => GetCatalog().GetOreBasePrice(oreType);

    public decimal GetSupplyBasePrice(SupplyType supplyType) => GetCatalog().GetSupplyBasePrice(supplyType);

    public decimal GetSupplyDailyConsumption(SupplyType supplyType) =>
        GetCatalog().GetSupplyDailyConsumption(supplyType);

    public string GetSupplyStockSymbol(SupplyType supplyType) => GetCatalog().GetSupplyStockSymbol(supplyType);

    public decimal GetReferenceClose(string stockSymbol) => GetCatalog().GetReferenceClose(stockSymbol);

    private MarketItemsCatalog GetCatalog()
    {
        var settings = options.CurrentValue;
        var path = RavaDataPaths.ResolveFile(environment.ContentRootPath, settings.ItemsFile);

        if (!File.Exists(path))
        {
            return _catalog;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (lastWriteUtc != _cachedWriteUtc)
        {
            _catalog = MarketItemsCsvLoader.LoadFromFile(path);
            _cachedWriteUtc = lastWriteUtc;
        }

        return _catalog;
    }
}
