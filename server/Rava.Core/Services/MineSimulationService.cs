using Rava.Core.Constants;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;

namespace Rava.Core.Services;

public class MineSimulationService(IMarketItemsCatalog marketItems) : IMineSimulationService
{
    private readonly IMarketItemsCatalog _marketItems = marketItems;

    public DayAdvanceResult AdvanceDay(
        PlayerState player,
        MineState mine,
        IReadOnlyList<InventoryItemState> inventory,
        DailyMarketSnapshot marketSnapshot)
    {
        var result = new DayAdvanceResult
        {
            MarketSnapshot = marketSnapshot,
            Messages = []
        };

        var oreExtracted = new Dictionary<string, decimal>();
        var hasSupplies = HasMinimumSupplies(inventory);

        foreach (var worker in mine.Workers.Where(w => w.AssignedZoneId.HasValue))
        {
            var zone = mine.Zones.FirstOrDefault(z => z.Id == worker.AssignedZoneId);
            if (zone is null || zone.DepletedPct >= 100m)
            {
                continue;
            }

            var efficiency = hasSupplies ? CalculateWorkerEfficiency(worker, inventory) : 0.25m;
            var extraction = worker.Skill * zone.Richness * efficiency * (1m - zone.DepletedPct / 200m);
            extraction = Math.Round(extraction, 2);

            if (extraction <= 0)
            {
                continue;
            }

            var oreKey = zone.OreType.ToString();
            oreExtracted[oreKey] = oreExtracted.GetValueOrDefault(oreKey) + extraction;
            AddInventory(inventory, player.Id, ItemCategory.Ore, oreKey, extraction);

            if (!zone.IsSalvageZone)
            {
                zone.DepletedPct = Math.Min(100m, zone.DepletedPct + extraction * 0.5m);
            }
        }

        result.OreExtracted = oreExtracted;

        var payroll = CalculateDailyPayroll(mine);
        player.Credits -= payroll;
        result.PayrollPaid = payroll;

        var supplyCost = ConsumeDailySupplies(inventory, marketSnapshot);
        player.Credits -= supplyCost;
        result.SuppliesConsumed = supplyCost;

        player.CurrentGameDay++;
        result.NewGameDay = player.CurrentGameDay;
        result.Credits = player.Credits;

        if (!hasSupplies)
        {
            result.Messages.Add("Supply stocks are low. Mining efficiency reduced to 25%.");
        }

        if (oreExtracted.Count == 0)
        {
            result.Messages.Add("No ore extracted. Assign workers to active zones.");
        }
        else
        {
            result.Messages.Add($"Extracted {string.Join(", ", oreExtracted.Select(kv => $"{kv.Value} {kv.Key}"))}.");
        }

        result.Messages.Add($"Paid {payroll} credits in payroll.");
        if (supplyCost > 0)
        {
            result.Messages.Add($"Consumed supplies worth {supplyCost} credits.");
        }

        return result;
    }

    public decimal CalculateDailyPayroll(MineState mine) =>
        mine.Workers.Sum(w => w.Salary);

    public decimal CalculateDailySupplyCost(IReadOnlyList<InventoryItemState> inventory, DailyMarketSnapshot market)
    {
        var cost = 0m;
        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            var price = market.Prices.First(p => p.SupplyType == supplyType).Price;
            cost += _marketItems.GetSupplyDailyConsumption(supplyType) * price;
        }

        return Math.Round(cost, 2);
    }

    public decimal CalculateEstimatedDailyIncome(MineState mine, IReadOnlyList<InventoryItemState> inventory)
    {
        var hasSupplies = HasMinimumSupplies(inventory);
        var total = 0m;

        foreach (var worker in mine.Workers.Where(w => w.AssignedZoneId.HasValue))
        {
            var zone = mine.Zones.FirstOrDefault(z => z.Id == worker.AssignedZoneId);
            if (zone is null || zone.DepletedPct >= 100m)
            {
                continue;
            }

            var efficiency = hasSupplies ? CalculateWorkerEfficiency(worker, inventory) : 0.25m;
            var extraction = worker.Skill * zone.Richness * efficiency * (1m - zone.DepletedPct / 200m);
            var orePrice = _marketItems.GetOreBasePrice(zone.OreType);
            total += extraction * orePrice;
        }

        return Math.Round(total, 2);
    }

    public FinanceSummary BuildFinanceSummary(
        PlayerState player,
        MineState mine,
        IReadOnlyList<InventoryItemState> inventory,
        IReadOnlyList<TransactionState> transactions,
        DailyMarketSnapshot market)
    {
        var payroll = CalculateDailyPayroll(mine);
        var supplyCost = CalculateDailySupplyCost(inventory, market);
        var estimatedIncome = CalculateEstimatedDailyIncome(mine, inventory);
        var dailyBurn = payroll + supplyCost - estimatedIncome;
        var runway = dailyBurn <= 0 ? 999m : Math.Max(0m, Math.Round(player.Credits / dailyBurn, 1));

        var hasSellableOre = inventory.Any(i =>
            i.Category == ItemCategory.Ore && i.Quantity > 0 &&
            Enum.TryParse<OreType>(i.ItemType, out _));

        return new FinanceSummary
        {
            Credits = player.Credits,
            DailyPayroll = payroll,
            DailySupplyCost = supplyCost,
            EstimatedDailyIncome = estimatedIncome,
            RunwayDays = runway,
            IsSoftlocked = IsSoftlocked(player, inventory),
            CanEmergencyBuyback = player.Credits <= 0 && hasSellableOre,
            RecentTransactions = transactions.OrderByDescending(t => t.CreatedAt).Take(20).ToList()
        };
    }

    public bool IsSoftlocked(PlayerState player, IReadOnlyList<InventoryItemState> inventory)
    {
        if (player.Credits > 0)
        {
            return false;
        }

        var hasSellableOre = inventory.Any(i =>
            i.Category == ItemCategory.Ore && i.Quantity > 0);

        return !hasSellableOre;
    }

    private bool HasMinimumSupplies(IReadOnlyList<InventoryItemState> inventory)
    {
        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            var item = inventory.FirstOrDefault(i =>
                i.Category == ItemCategory.Supply && i.ItemType == supplyType.ToString());
            var required = _marketItems.GetSupplyDailyConsumption(supplyType);
            if (item is null || item.Quantity < required)
            {
                return false;
            }
        }

        return true;
    }

    private static decimal CalculateWorkerEfficiency(WorkerState worker, IReadOnlyList<InventoryItemState> inventory)
    {
        var drillBits = GetSupplyQuantity(inventory, SupplyType.DrillBits);
        var lifeSupport = GetSupplyQuantity(inventory, SupplyType.LifeSupport);
        var drillBonus = Math.Min(1m, drillBits / 5m) * 0.2m;
        var lifeBonus = Math.Min(1m, lifeSupport / 5m) * 0.15m;
        return 1m + drillBonus + lifeBonus + worker.Skill * 0.05m;
    }

    private static decimal GetSupplyQuantity(IReadOnlyList<InventoryItemState> inventory, SupplyType supplyType)
    {
        return inventory.FirstOrDefault(i =>
            i.Category == ItemCategory.Supply && i.ItemType == supplyType.ToString())?.Quantity ?? 0m;
    }

    private decimal ConsumeDailySupplies(IReadOnlyList<InventoryItemState> inventory, DailyMarketSnapshot market)
    {
        var totalCost = 0m;
        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            var consumption = _marketItems.GetSupplyDailyConsumption(supplyType);
            var item = inventory.FirstOrDefault(i =>
                i.Category == ItemCategory.Supply && i.ItemType == supplyType.ToString());
            if (item is null)
            {
                continue;
            }

            var consumed = Math.Min(item.Quantity, consumption);
            item.Quantity -= consumed;
            var price = market.Prices.First(p => p.SupplyType == supplyType).Price;
            totalCost += consumed * price;
        }

        return Math.Round(totalCost, 2);
    }

    private static void AddInventory(
        IReadOnlyList<InventoryItemState> inventory,
        Guid playerId,
        ItemCategory category,
        string itemType,
        decimal quantity)
    {
        var item = inventory.FirstOrDefault(i => i.Category == category && i.ItemType == itemType);
        if (item is null)
        {
            ((List<InventoryItemState>)inventory).Add(new InventoryItemState
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Category = category,
                ItemType = itemType,
                Quantity = quantity
            });
        }
        else
        {
            item.Quantity += quantity;
        }
    }
}
