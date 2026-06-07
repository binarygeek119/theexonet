using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;

namespace Theexonet.Core.Services;

public static class CompanyValueCalculator
{
    public static decimal Compute(decimal credits, IEnumerable<InventoryItemSnapshot> inventory, IMarketItemsCatalog market)
    {
        var assets = 0m;

        foreach (var item in inventory)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }

            if (item.Category == ItemCategory.Ore &&
                Enum.TryParse<OreType>(item.ItemType, out var oreType))
            {
                assets += item.Quantity * market.GetOreBasePrice(oreType);
            }
            else if (item.Category == ItemCategory.Supply &&
                     Enum.TryParse<SupplyType>(item.ItemType, out var supplyType))
            {
                assets += item.Quantity * market.GetSupplyBasePrice(supplyType);
            }
        }

        return Math.Round(credits + assets, 2);
    }
}

public readonly record struct InventoryItemSnapshot(ItemCategory Category, string ItemType, decimal Quantity);
