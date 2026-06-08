using Theexonet.Core.Enums;

namespace Theexonet.Core.Constants;

public static class GameBalance
{
    public const decimal StarterCredits = GameCredits.SignUp;
    public const decimal BirthdayBonusCredits = GameCredits.BirthdayBonus;
    public const decimal OperatingStarterFloat = 1000m;
    public const decimal OperatingMigrationCap = 1500m;
    public const decimal AsteroidMinerDailySalary = 120m;
    public const decimal EmergencyBuybackRate = 0.5m;
    public const int GridSize = 8;
    public const int StarterWorkerCount = 5;
    public const int StarterSupplyQuantity = 10;
    public const int MaxMineWorkers = 12;
    public const decimal HireFee = 350m;
    public const decimal LayoffSeveranceBase = 80m;
    public const decimal FireSeveranceBase = 200m;
    public const decimal LayoffSeveranceSalaryMultiplier = 0.5m;
    public const decimal FireSeveranceSalaryMultiplier = 1.5m;
    public const decimal CompanyTaxRate = 0.08m;
    public const decimal HealthInsurancePerWorker = 12m;
    public const decimal JobInsurancePerWorker = 8m;
    public const decimal BeltOperatingFee = 25m;
    public const int MiningRightsPeriodDays = 30;
    public const decimal MiningRightsRenewalFee = 900m;
    public const decimal MiningRightsExpiredDailyPenalty = 50m;
    public const decimal HireSalaryMin = 80m;
    public const decimal HireSalaryMax = 160m;
    public const int HireSkillMin = 1;
    public const int HireSkillMax = 4;
    public const decimal MaxWorkerSalary = 300m;

    public const decimal MaxCondition = 100m;
    public const decimal SupplyWearPerConsumption = 18m;
    public const decimal OreStorageWearPerDay = 2.5m;
    public const decimal LowConditionThreshold = 25m;
    public const decimal LowConditionBreakChancePerDay = 0.22m;
    public const int MinTradeListingsPerItemType = 2;
    public const decimal NpcListingDiscount = 0.88m;
    public const int NpcConditionMin = 45;
    public const int NpcConditionMax = 85;
    public const decimal TradeListingFeePercent = 5m;

    public static decimal LayoffSeverance(decimal salary) =>
        LayoffSeveranceBase + Math.Round(salary * LayoffSeveranceSalaryMultiplier, 2);

    public static decimal FireSeverance(decimal salary) =>
        FireSeveranceBase + Math.Round(salary * FireSeveranceSalaryMultiplier, 2);

    public static readonly IReadOnlyDictionary<OreType, decimal> BaseOrePrices = new Dictionary<OreType, decimal>
    {
        [OreType.Ferroxite] = 120m,
        [OreType.Voidium] = 280m,
        [OreType.Stellarite] = 450m,
        [OreType.SalvageScrap] = 40m
    };

    public static readonly IReadOnlyDictionary<SupplyType, decimal> BaseSupplyPrices = new Dictionary<SupplyType, decimal>
    {
        [SupplyType.DrillBits] = 85m,
        [SupplyType.FuelCells] = 110m,
        [SupplyType.LifeSupport] = 95m,
        [SupplyType.CommModules] = 130m
    };

    public static readonly IReadOnlyDictionary<SupplyType, decimal> SupplyConsumptionPerDay = new Dictionary<SupplyType, decimal>
    {
        [SupplyType.DrillBits] = 0.5m,
        [SupplyType.FuelCells] = 0.3m,
        [SupplyType.LifeSupport] = 0.4m,
        [SupplyType.CommModules] = 0.2m
    };

    public const decimal OreStockpileCapacity = 100m;
    public const decimal MinEfficientShipmentFill = 0.70m;
    public const decimal UnderfillCostPenaltyMultiplier = 1.35m;

    public static readonly IReadOnlyDictionary<ShipClass, decimal> ShipCapacity = new Dictionary<ShipClass, decimal>
    {
        [ShipClass.Scout] = 25m,
        [ShipClass.Hauler] = 55m,
        [ShipClass.Freighter] = 110m,
        [ShipClass.Bulk] = 220m,
    };

    public static readonly IReadOnlyDictionary<ShipClass, decimal> ShipBaseCostMultiplier = new Dictionary<ShipClass, decimal>
    {
        [ShipClass.Scout] = 1.15m,
        [ShipClass.Hauler] = 1m,
        [ShipClass.Freighter] = 0.92m,
        [ShipClass.Bulk] = 0.85m,
    };

    public static readonly IReadOnlyDictionary<ShippingRouteTier, int> RouteTransitDays = new Dictionary<ShippingRouteTier, int>
    {
        [ShippingRouteTier.Express] = 2,
        [ShippingRouteTier.Standard] = 4,
        [ShippingRouteTier.Economy] = 7,
    };

    public static readonly IReadOnlyDictionary<ShippingRouteTier, decimal> RouteFastLegPercent = new Dictionary<ShippingRouteTier, decimal>
    {
        [ShippingRouteTier.Express] = 0.45m,
        [ShippingRouteTier.Standard] = 0.25m,
        [ShippingRouteTier.Economy] = 0m,
    };

    public static readonly IReadOnlyDictionary<ShippingRouteTier, decimal> RouteCostPerUnit = new Dictionary<ShippingRouteTier, decimal>
    {
        [ShippingRouteTier.Express] = 9.5m,
        [ShippingRouteTier.Standard] = 4.8m,
        [ShippingRouteTier.Economy] = 2.4m,
    };

    public const decimal ExpressMinCargoForEfficiency = 35m;
    public const decimal TransitRobberyChancePerDay = 0.03m;
    public const decimal TransitCrashChancePerDay = 0.008m;
    public const decimal TransitDelayChancePerDay = 0.12m;
    public const decimal RobberyCargoLossPercent = 0.25m;
}
