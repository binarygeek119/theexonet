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
        DailyMarketSnapshot marketSnapshot);

    decimal CalculateDailyPayroll(MineState mine);
    decimal CalculateDailySupplyCost(IReadOnlyList<InventoryItemState> inventory, DailyMarketSnapshot market);
    decimal CalculateEstimatedDailyIncome(MineState mine, IReadOnlyList<InventoryItemState> inventory);
    FinanceSummary BuildFinanceSummary(
        PlayerState player,
        MineState mine,
        IReadOnlyList<InventoryItemState> inventory,
        IReadOnlyList<TransactionState> transactions,
        DailyMarketSnapshot market);
    bool IsSoftlocked(PlayerState player, IReadOnlyList<InventoryItemState> inventory);
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
