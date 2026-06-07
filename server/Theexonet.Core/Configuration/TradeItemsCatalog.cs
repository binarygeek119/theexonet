using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;

namespace Theexonet.Core.Configuration;

public sealed class TradeItemsCatalog : ITradeItemsCatalog
{
    private readonly Dictionary<OreType, TradeItemDefinition> _oreItems;
    private readonly Dictionary<SupplyType, TradeItemDefinition> _supplyItems;

    public TradeItemsCatalog(
        Dictionary<OreType, TradeItemDefinition> oreItems,
        Dictionary<SupplyType, TradeItemDefinition> supplyItems)
    {
        _oreItems = oreItems;
        _supplyItems = supplyItems;
    }

    public IReadOnlyList<TradeItemDefinition> GetAllItems() =>
        _oreItems.Values.Concat<TradeItemDefinition>(_supplyItems.Values).ToList();

    public IReadOnlyList<TradeItemDefinition> GetOreItems() => _oreItems.Values.ToList();

    public IReadOnlyList<TradeItemDefinition> GetSupplyItems() => _supplyItems.Values.ToList();

    public bool IsTradeableOre(OreType oreType) => _oreItems.ContainsKey(oreType);

    public bool IsTradeableSupply(SupplyType supplyType) => _supplyItems.ContainsKey(supplyType);

    public TradeItemDefinition GetOreItem(OreType oreType) =>
        _oreItems.TryGetValue(oreType, out var item)
            ? item
            : throw new KeyNotFoundException($"Trade ore item not configured for {oreType}.");

    public TradeItemDefinition GetSupplyItem(SupplyType supplyType) =>
        _supplyItems.TryGetValue(supplyType, out var item)
            ? item
            : throw new KeyNotFoundException($"Trade supply item not configured for {supplyType}.");

    public static TradeItemsCatalog CreateDefault()
    {
        var oreItems = new Dictionary<OreType, TradeItemDefinition>
        {
            [OreType.Ferroxite] = CreateOre(OreType.Ferroxite, "Ferroxite", "#996644", 120m),
            [OreType.Voidium] = CreateOre(OreType.Voidium, "Voidium", "#6633b3", 280m),
            [OreType.Stellarite] = CreateOre(OreType.Stellarite, "Stellarite", "#e6cc4d", 450m),
            [OreType.SalvageScrap] = CreateOre(OreType.SalvageScrap, "Salvage Scrap", "#80808c", 40m, true)
        };

        var supplyItems = new Dictionary<SupplyType, TradeItemDefinition>
        {
            [SupplyType.DrillBits] = CreateSupply(SupplyType.DrillBits, "Drill Bits", "#b38033", 85m, "XLI"),
            [SupplyType.FuelCells] = CreateSupply(SupplyType.FuelCells, "Fuel Cells", "#3399e6", 110m, "XLE"),
            [SupplyType.LifeSupport] = CreateSupply(SupplyType.LifeSupport, "Life Support", "#4dcc80", 95m, "XLV"),
            [SupplyType.CommModules] = CreateSupply(SupplyType.CommModules, "Comm Modules", "#80b3ff", 130m, "XLK")
        };

        return new TradeItemsCatalog(oreItems, supplyItems);
    }

    private static TradeItemDefinition CreateOre(
        OreType oreType,
        string displayName,
        string color,
        decimal basePrice,
        bool isEmergencySource = false) =>
        new()
        {
            Category = ItemCategory.Ore,
            ItemType = oreType.ToString(),
            DisplayName = displayName,
            Color = color,
            BasePrice = basePrice,
            IsEmergencySource = isEmergencySource
        };

    private static TradeItemDefinition CreateSupply(
        SupplyType supplyType,
        string displayName,
        string color,
        decimal basePrice,
        string uiSymbol) =>
        new()
        {
            Category = ItemCategory.Supply,
            ItemType = supplyType.ToString(),
            DisplayName = displayName,
            Color = color,
            BasePrice = basePrice,
            UiSymbol = uiSymbol
        };
}
