using Theexonet.Core.Models;

namespace Theexonet.Core.Interfaces;

public interface IMarketDataProvider
{
    Task<DailyMarketSnapshot> GetDailyPricesAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken cancellationToken = default);
}

public interface IMineSimulationService
{
    DayAdvanceResult AdvanceDay(
        PlayerState player,
        MineState mine,
        IReadOnlyList<InventoryItemState> inventory,
        IList<MineStockpileState> stockpile,
        DailyMarketSnapshot marketSnapshot);

    decimal GetTotalStockpileQuantity(IEnumerable<MineStockpileState> stockpile);

    decimal CalculateDailyPayroll(MineState mine);
    decimal CalculateDailySupplyCost(IReadOnlyList<InventoryItemState> inventory, DailyMarketSnapshot market);
    decimal CalculateEstimatedDailyIncome(MineState mine, IReadOnlyList<InventoryItemState> inventory);
    FinanceSummary BuildFinanceSummary(
        PlayerState player,
        MineState mine,
        IReadOnlyList<InventoryItemState> inventory,
        IReadOnlyList<TransactionState> transactions,
        DailyMarketSnapshot market,
        decimal reserveBalance,
        decimal dailyJobSalary,
        decimal dailyCompanyObligations);
    bool IsSoftlocked(PlayerState player, IReadOnlyList<InventoryItemState> inventory, decimal reserveBalance);
}

public interface IStarterMineGenerator
{
    (MineState Mine, List<InventoryItemState> StarterInventory) Generate(Guid playerId, int asteroidSeed);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string GenerateToken(Guid playerId, string username);
}
