using Rava.Core.Enums;

namespace Rava.Core.Models;

public class PlayerState
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal Credits { get; set; }
    public int CurrentGameDay { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MineState
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AsteroidSeed { get; set; }
    public MineStatus Status { get; set; }
    public DateTime PurchasedAt { get; set; }
    public List<MineZoneState> Zones { get; set; } = [];
    public List<WorkerState> Workers { get; set; } = [];
}

public class MineZoneState
{
    public Guid Id { get; set; }
    public Guid MineId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public OreType OreType { get; set; }
    public decimal Richness { get; set; }
    public decimal DepletedPct { get; set; }
    public bool IsSalvageZone { get; set; }
}

public class WorkerState
{
    public Guid Id { get; set; }
    public Guid MineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Skill { get; set; }
    public decimal Salary { get; set; }
    public Guid? AssignedZoneId { get; set; }
}

public class InventoryItemState
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public ItemCategory Category { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class TransactionState
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int GameDay { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MarketPriceEntry
{
    public SupplyType SupplyType { get; set; }
    public decimal Price { get; set; }
    public decimal ChangePct { get; set; }
}

public class DailyMarketSnapshot
{
    public int GameDay { get; set; }
    public DateOnly Date { get; set; }
    public string Source { get; set; } = "mock";
    public List<MarketPriceEntry> Prices { get; set; } = [];
}

public class GameWorldState
{
    public int CurrentDay { get; set; } = 1;
    public DateTime LastTickAt { get; set; } = DateTime.UtcNow;
    public int MarketSeed { get; set; } = 42;
}

public class FeatureFlags
{
    public bool Trading { get; set; }
    public bool Friends { get; set; }
    public bool MultiMine { get; set; }
    public bool MineGroups { get; set; }
    public bool SpecialDeals { get; set; }
    public bool AccountNuke { get; set; }

    public static FeatureFlags Phase1 => new()
    {
        Trading = false,
        Friends = false,
        MultiMine = false,
        MineGroups = false,
        SpecialDeals = false,
        AccountNuke = false
    };
}

public class FinanceSummary
{
    public decimal Credits { get; set; }
    public decimal DailyPayroll { get; set; }
    public decimal DailySupplyCost { get; set; }
    public decimal EstimatedDailyIncome { get; set; }
    public decimal RunwayDays { get; set; }
    public bool IsSoftlocked { get; set; }
    public bool CanEmergencyBuyback { get; set; }
    public List<TransactionState> RecentTransactions { get; set; } = [];
}

public class DayAdvanceResult
{
    public int NewGameDay { get; set; }
    public decimal Credits { get; set; }
    public Dictionary<string, decimal> OreExtracted { get; set; } = new();
    public decimal PayrollPaid { get; set; }
    public decimal SuppliesConsumed { get; set; }
    public DailyMarketSnapshot MarketSnapshot { get; set; } = new();
    public List<string> Messages { get; set; } = [];
}
