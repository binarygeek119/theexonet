using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;

namespace Theexonet.Infrastructure.Services;

public class TradeItemsProvider(
    IWebHostEnvironment environment,
    IOptionsMonitor<TradeOptions> options) : ITradeItemsCatalog
{
    private TradeItemsCatalog _catalog = TradeItemsCatalog.CreateDefault();
    private DateTime _cachedWriteUtc = DateTime.MinValue;

    public IReadOnlyList<TradeItemDefinition> GetAllItems() => GetCatalog().GetAllItems();

    public IReadOnlyList<TradeItemDefinition> GetOreItems() => GetCatalog().GetOreItems();

    public IReadOnlyList<TradeItemDefinition> GetSupplyItems() => GetCatalog().GetSupplyItems();

    public bool IsTradeableOre(OreType oreType) => GetCatalog().IsTradeableOre(oreType);

    public bool IsTradeableSupply(SupplyType supplyType) => GetCatalog().IsTradeableSupply(supplyType);

    public TradeItemDefinition GetOreItem(OreType oreType) => GetCatalog().GetOreItem(oreType);

    public TradeItemDefinition GetSupplyItem(SupplyType supplyType) => GetCatalog().GetSupplyItem(supplyType);

    private TradeItemsCatalog GetCatalog()
    {
        var settings = options.CurrentValue;
        var path = TheexonetDataPaths.ResolveFile(environment.ContentRootPath, settings.ItemsFile);

        if (!File.Exists(path))
        {
            return _catalog;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (lastWriteUtc != _cachedWriteUtc)
        {
            _catalog = TradeItemsCsvLoader.LoadFromFile(path);
            _cachedWriteUtc = lastWriteUtc;
        }

        return _catalog;
    }
}
