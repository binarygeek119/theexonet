using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Models;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ItemConditionCalculatorTests
{
    [Fact]
    public void ConditionPriceFactor_ScalesWithCondition()
    {
        Assert.True(ItemConditionCalculator.ConditionPriceFactor(100) > ItemConditionCalculator.ConditionPriceFactor(50));
        Assert.Equal(0.75m, ItemConditionCalculator.ConditionPriceFactor(50));
    }

    [Fact]
    public void ApplySupplyWear_ReducesCondition()
    {
        var item = new InventoryItemState
        {
            Category = ItemCategory.Supply,
            ItemType = nameof(SupplyType.DrillBits),
            Quantity = 5m,
            Condition = 100m,
        };

        var result = ItemConditionCalculator.ApplySupplyWear(item, 0.5m, gameDay: 1, rng: new Random(1));

        Assert.True(item.Condition < 100m);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void ApplySupplyWear_BreaksUnitAtZeroCondition()
    {
        var item = new InventoryItemState
        {
            Category = ItemCategory.Supply,
            ItemType = nameof(SupplyType.FuelCells),
            Quantity = 3m,
            Condition = 5m,
        };

        var result = ItemConditionCalculator.ApplySupplyWear(item, 0.3m, gameDay: 2, rng: new Random(99));

        Assert.True(result.UnitBroke || item.BrokenQuantity > 0 || item.Condition <= 0);
    }

    [Fact]
    public void MergeCondition_WeightedAverage()
    {
        var merged = ItemConditionCalculator.MergeCondition(4m, 80m, 2m, 100m);
        Assert.Equal(86.7m, merged);
    }
}
