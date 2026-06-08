using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public sealed class ConditionWearResult
{
    public List<string> Messages { get; } = [];
    public bool UnitBroke { get; set; }
}

public static class ItemConditionCalculator
{
    public static decimal MaxCondition => GameBalance.MaxCondition;

    public static decimal ConditionPriceFactor(decimal condition) =>
        0.5m + 0.5m * Math.Clamp(condition, 0m, MaxCondition) / MaxCondition;

    public static decimal EffectiveSupplyQuantity(InventoryItemState? item)
    {
        if (item is null || item.Quantity <= 0 || item.Condition <= 0)
        {
            return 0m;
        }

        return item.Quantity;
    }

    public static ConditionWearResult ApplySupplyWear(
        InventoryItemState item,
        decimal consumed,
        int gameDay,
        Random? rng = null)
    {
        var result = new ConditionWearResult();
        if (item.Quantity <= 0 || consumed <= 0)
        {
            return result;
        }

        var wear = Math.Round(GameBalance.SupplyWearPerConsumption * consumed, 2);
        item.Condition = Math.Max(0m, item.Condition - wear);

        if (item.Condition < GameBalance.LowConditionThreshold)
        {
            result.Messages.Add($"{item.ItemType} condition low ({item.Condition:0}%).");
        }

        TryBreakUnit(item, gameDay, rng ?? Random.Shared, result);
        return result;
    }

    public static ConditionWearResult ApplyOreStorageWear(
        InventoryItemState item,
        int gameDay,
        Random? rng = null)
    {
        var result = new ConditionWearResult();
        if (item.Quantity <= 0)
        {
            return result;
        }

        item.Condition = Math.Max(0m, item.Condition - GameBalance.OreStorageWearPerDay);
        TryBreakUnit(item, gameDay, rng ?? Random.Shared, result);
        return result;
    }

    public static decimal MergeCondition(decimal existingQty, decimal existingCondition, decimal addQty, decimal addCondition)
    {
        if (existingQty + addQty <= 0)
        {
            return MaxCondition;
        }

        return Math.Round(
            (existingQty * existingCondition + addQty * addCondition) / (existingQty + addQty),
            1);
    }

    private static void TryBreakUnit(InventoryItemState item, int gameDay, Random rng, ConditionWearResult result)
    {
        if (item.Quantity < 1m)
        {
            return;
        }

        if (item.Condition <= 0)
        {
            BreakOneUnit(item, result);
            return;
        }

        if (item.Condition >= GameBalance.LowConditionThreshold)
        {
            return;
        }

        var breakChance = GameBalance.LowConditionBreakChancePerDay
            * (1m - item.Condition / GameBalance.LowConditionThreshold);
        if (rng.NextDouble() < (double)breakChance)
        {
            BreakOneUnit(item, result);
        }
    }

    private static void BreakOneUnit(InventoryItemState item, ConditionWearResult result)
    {
        if (item.Quantity < 1m)
        {
            item.Quantity = 0;
            item.Condition = 0;
            return;
        }

        item.Quantity -= 1m;
        item.BrokenQuantity += 1m;
        item.Condition = MaxCondition;
        result.UnitBroke = true;
        result.Messages.Add($"1 {item.ItemType} unit broke — replace from Store.");
    }
}
