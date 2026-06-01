using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class CompanyValueCalculatorTests
{
    private sealed class StubMarket : IMarketItemsCatalog
    {
        public decimal GetOreBasePrice(OreType oreType) => oreType switch
        {
            OreType.Ferroxite => 120m,
            OreType.Voidium => 280m,
            _ => 40m,
        };

        public decimal GetSupplyBasePrice(SupplyType supplyType) => supplyType switch
        {
            SupplyType.DrillBits => 85m,
            _ => 50m,
        };

        public decimal GetSupplyDailyConsumption(SupplyType supplyType) => 1m;

        public string GetSupplyStockSymbol(SupplyType supplyType) => "XLI";

        public decimal GetReferenceClose(string stockSymbol) => 100m;
    }

    [Fact]
    public void Compute_includes_credits_and_inventory_at_base_prices()
    {
        var market = new StubMarket();
        var inventory = new[]
        {
            new InventoryItemSnapshot(ItemCategory.Ore, OreType.Ferroxite.ToString(), 2m),
            new InventoryItemSnapshot(ItemCategory.Supply, SupplyType.DrillBits.ToString(), 1m),
        };

        var value = CompanyValueCalculator.Compute(1000m, inventory, market);

        Assert.Equal(1325m, value);
    }

    [Fact]
    public void Compute_ignores_zero_quantity_items()
    {
        var market = new StubMarket();
        var inventory = new[]
        {
            new InventoryItemSnapshot(ItemCategory.Ore, OreType.Ferroxite.ToString(), 0m),
        };

        var value = CompanyValueCalculator.Compute(50m, inventory, market);

        Assert.Equal(50m, value);
    }
}
