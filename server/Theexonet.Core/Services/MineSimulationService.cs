using Theexonet.Core.Constants;

using Theexonet.Core.Enums;

using Theexonet.Core.Interfaces;

using Theexonet.Core.Models;



namespace Theexonet.Core.Services;



public class MineSimulationService(IMarketItemsCatalog marketItems) : IMineSimulationService

{

    private readonly IMarketItemsCatalog _marketItems = marketItems;



    public decimal GetTotalStockpileQuantity(IEnumerable<MineStockpileState> stockpile) =>

        stockpile.Sum(s => s.Quantity);



    public DayAdvanceResult AdvanceDay(

        PlayerState player,

        MineState mine,

        IReadOnlyList<InventoryItemState> inventory,

        IList<MineStockpileState> stockpile,

        DailyMarketSnapshot marketSnapshot)

    {

        var result = new DayAdvanceResult

        {

            MarketSnapshot = marketSnapshot,

            Messages = []

        };



        var oreExtracted = new Dictionary<string, decimal>();

        var hasSupplies = HasMinimumSupplies(inventory);

        var wearRng = new Random(player.Id.GetHashCode() ^ player.CurrentGameDay);

        var stockpileFull = GetTotalStockpileQuantity(stockpile) >= GameBalance.OreStockpileCapacity;



        foreach (var worker in mine.Workers.Where(w => w.AssignedZoneId.HasValue))

        {

            var zone = mine.Zones.FirstOrDefault(z => z.Id == worker.AssignedZoneId);

            if (zone is null || zone.DepletedPct >= 100m)

            {

                continue;

            }



            if (stockpileFull)

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

            var remainingCapacity = GameBalance.OreStockpileCapacity - GetTotalStockpileQuantity(stockpile);

            if (remainingCapacity <= 0)

            {

                stockpileFull = true;

                break;

            }



            if (extraction > remainingCapacity)

            {

                extraction = Math.Round(remainingCapacity, 2);

            }



            oreExtracted[oreKey] = oreExtracted.GetValueOrDefault(oreKey) + extraction;

            AddToStockpile(stockpile, mine.Id, oreKey, extraction);



            if (!zone.IsSalvageZone)

            {

                zone.DepletedPct = Math.Min(100m, zone.DepletedPct + extraction * 0.5m);

            }



            stockpileFull = GetTotalStockpileQuantity(stockpile) >= GameBalance.OreStockpileCapacity;

        }



        result.OreExtracted = oreExtracted;



        var payroll = CalculateDailyPayroll(mine);

        result.PayrollPaid = payroll;



        var supplyCost = ConsumeDailySupplies(inventory, marketSnapshot, player.CurrentGameDay, wearRng, result.Messages);

        ApplyOreStorageWear(inventory, player.CurrentGameDay, wearRng, result.Messages);

        ApplyStockpileStorageWear(stockpile, player.CurrentGameDay, wearRng, result.Messages);



        player.Credits -= supplyCost;

        result.SuppliesConsumed = supplyCost;



        player.CurrentGameDay++;

        result.NewGameDay = player.CurrentGameDay;

        result.Credits = player.Credits;

        result.ReserveBalance = player.ReserveBalance;



        if (stockpileFull)

        {

            result.Messages.Add("Ore stockpile full — mining halted until shipments clear the pad.");

        }



        if (!hasSupplies)

        {

            result.Messages.Add("Supply stocks are low. Mining efficiency reduced to 25%.");

        }



        if (oreExtracted.Count == 0 && !stockpileFull)

        {

            result.Messages.Add("No ore extracted. Assign workers to active zones.");

        }

        else if (oreExtracted.Count > 0)

        {

            result.Messages.Add($"Extracted {string.Join(", ", oreExtracted.Select(kv => $"{kv.Value} {kv.Key}"))} to stockpile.");

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

        DailyMarketSnapshot market,

        decimal reserveBalance,

        decimal dailyJobSalary,

        decimal dailyCompanyObligations)

    {

        var payroll = CalculateDailyPayroll(mine);

        var supplyCost = CalculateDailySupplyCost(inventory, market);

        var estimatedIncome = CalculateEstimatedDailyIncome(mine, inventory);

        var dailyTotalReserveBurn = payroll + dailyCompanyObligations - dailyJobSalary;

        var dailyBurn = dailyTotalReserveBurn + supplyCost - estimatedIncome;

        var combinedLiquidity = player.Credits + reserveBalance;

        var runway = dailyBurn <= 0 ? 999m : Math.Max(0m, Math.Round(combinedLiquidity / dailyBurn, 1));



        var hasSellableOre = inventory.Any(i =>

            i.Category == ItemCategory.Ore && i.Quantity > 0 && i.Condition > 0 &&

            Enum.TryParse<OreType>(i.ItemType, out _));



        return new FinanceSummary

        {

            Credits = player.Credits,

            ReserveBalance = reserveBalance,

            DailyJobSalary = dailyJobSalary,

            DailyPayroll = payroll,

            DailyCompanyObligations = dailyCompanyObligations,

            DailyTotalReserveBurn = dailyTotalReserveBurn,

            DailySupplyCost = supplyCost,

            EstimatedDailyIncome = estimatedIncome,

            RunwayDays = runway,

            IsSoftlocked = IsSoftlocked(player, inventory, reserveBalance),

            CanEmergencyBuyback = player.Credits <= 0 && hasSellableOre,

            RecentTransactions = transactions.OrderByDescending(t => t.CreatedAt).Take(20).ToList()

        };

    }



    public bool IsSoftlocked(PlayerState player, IReadOnlyList<InventoryItemState> inventory, decimal reserveBalance)

    {

        if (player.Credits > 0 || reserveBalance > 0)

        {

            return false;

        }



        var hasSellableOre = inventory.Any(i =>

            i.Category == ItemCategory.Ore && i.Quantity > 0 && i.Condition > 0);



        return !hasSellableOre;

    }



    private bool HasMinimumSupplies(IReadOnlyList<InventoryItemState> inventory)

    {

        foreach (var supplyType in Enum.GetValues<SupplyType>())

        {

            var required = _marketItems.GetSupplyDailyConsumption(supplyType);

            var usable = GetUsableSupplyQuantity(inventory, supplyType);

            if (usable < required)

            {

                return false;

            }

        }



        return true;

    }



    private static decimal CalculateWorkerEfficiency(WorkerState worker, IReadOnlyList<InventoryItemState> inventory)

    {

        var drillBits = GetEffectiveSupplyQuantity(inventory, SupplyType.DrillBits);

        var lifeSupport = GetEffectiveSupplyQuantity(inventory, SupplyType.LifeSupport);

        var drillBonus = Math.Min(1m, drillBits / 5m) * 0.2m;

        var lifeBonus = Math.Min(1m, lifeSupport / 5m) * 0.15m;

        return 1m + drillBonus + lifeBonus + worker.Skill * 0.05m;

    }



    private static decimal GetUsableSupplyQuantity(IReadOnlyList<InventoryItemState> inventory, SupplyType supplyType) =>

        inventory

            .Where(i => i.Category == ItemCategory.Supply

                && i.ItemType == supplyType.ToString()

                && i.Condition > 0

                && i.Quantity > 0)

            .Sum(i => i.Quantity);



    private static decimal GetEffectiveSupplyQuantity(IReadOnlyList<InventoryItemState> inventory, SupplyType supplyType) =>

        inventory

            .Where(i => i.Category == ItemCategory.Supply

                && i.ItemType == supplyType.ToString()

                && i.Condition > 0

                && i.Quantity > 0)

            .Sum(i => i.Quantity * (i.Condition / GameBalance.MaxCondition));



    private decimal ConsumeDailySupplies(

        IReadOnlyList<InventoryItemState> inventory,

        DailyMarketSnapshot market,

        int gameDay,

        Random rng,

        List<string> messages)

    {

        var totalCost = 0m;

        foreach (var supplyType in Enum.GetValues<SupplyType>())

        {

            var consumption = _marketItems.GetSupplyDailyConsumption(supplyType);

            var remaining = consumption;

            var stacks = inventory

                .Where(i => i.Category == ItemCategory.Supply

                    && i.ItemType == supplyType.ToString()

                    && i.Condition > 0

                    && i.Quantity > 0)

                .OrderBy(i => i.IsNew)

                .ToList();



            foreach (var item in stacks)

            {

                if (remaining <= 0)

                {

                    break;

                }



                var consumed = Math.Min(item.Quantity, remaining);

                item.Quantity -= consumed;

                remaining -= consumed;



                var price = market.Prices.First(p => p.SupplyType == supplyType).Price;

                totalCost += consumed * price;



                var wear = ItemConditionCalculator.ApplySupplyWear(item, consumed, gameDay, rng);

                messages.AddRange(wear.Messages);

            }

        }



        return Math.Round(totalCost, 2);

    }



    private static void ApplyOreStorageWear(

        IReadOnlyList<InventoryItemState> inventory,

        int gameDay,

        Random rng,

        List<string> messages)

    {

        foreach (var item in inventory.Where(i => i.Category == ItemCategory.Ore && i.Quantity > 0))

        {

            var wear = ItemConditionCalculator.ApplyOreStorageWear(item, gameDay, rng);

            messages.AddRange(wear.Messages);

        }

    }



    private static void ApplyStockpileStorageWear(

        IList<MineStockpileState> stockpile,

        int gameDay,

        Random rng,

        List<string> messages)

    {

        foreach (var item in stockpile.Where(s => s.Quantity > 0))

        {

            var temp = new InventoryItemState

            {

                ItemType = item.OreType,

                Quantity = item.Quantity,

                Condition = item.Condition,

            };

            var wear = ItemConditionCalculator.ApplyOreStorageWear(temp, gameDay, rng);

            item.Condition = temp.Condition;

            item.Quantity = temp.Quantity;

            messages.AddRange(wear.Messages);

        }

    }



    private static void AddToStockpile(

        IList<MineStockpileState> stockpile,

        Guid mineId,

        string oreType,

        decimal quantity)

    {

        var item = stockpile.FirstOrDefault(s => s.OreType == oreType);

        if (item is null)

        {

            stockpile.Add(new MineStockpileState

            {

                Id = Guid.NewGuid(),

                MineId = mineId,

                OreType = oreType,

                Quantity = quantity,

                Condition = GameBalance.MaxCondition,

            });

        }

        else

        {

            item.Condition = ItemConditionCalculator.MergeCondition(

                item.Quantity, item.Condition, quantity, GameBalance.MaxCondition);

            item.Quantity += quantity;

        }

    }

}


