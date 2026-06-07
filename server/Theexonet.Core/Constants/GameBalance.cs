using Theexonet.Core.Enums;

namespace Theexonet.Core.Constants;

public static class GameBalance
{
    public const decimal StarterCredits = GameCredits.SignUp;
    public const decimal BirthdayBonusCredits = GameCredits.BirthdayBonus;
    public const decimal EmergencyBuybackRate = 0.5m;
    public const int GridSize = 8;
    public const int StarterWorkerCount = 5;
    public const int StarterSupplyQuantity = 10;

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
}
