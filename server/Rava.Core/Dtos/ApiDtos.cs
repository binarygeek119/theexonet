using Rava.Core.Models;

namespace Rava.Core.Dtos;

public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string Token, Guid PlayerId, Guid MineId, string Username, FeatureFlags Features);

public record AssignWorkerRequest(Guid WorkerId, string? ZoneId);
public record BuySupplyRequest(SupplyTypeDto SupplyType, decimal Quantity);
public record SellOreRequest(OreTypeDto OreType, decimal Quantity, bool EmergencyBuyback = false);

public enum SupplyTypeDto
{
    DrillBits,
    FuelCells,
    LifeSupport,
    CommModules
}

public enum OreTypeDto
{
    Ferroxite,
    Voidium,
    Stellarite,
    SalvageScrap
}

public record MineZoneDto(Guid Id, int X, int Y, OreTypeDto OreType, decimal Richness, decimal DepletedPct, bool IsSalvageZone);
public record WorkerDto(Guid Id, string Name, int Skill, decimal Salary, Guid? AssignedZoneId);
public record InventoryItemDto(string ItemType, string Category, decimal Quantity);
public record TransactionDto(string Type, decimal Amount, string Description, int GameDay, DateTime CreatedAt);

public record MineDetailResponse(
    Guid Id,
    string Name,
    int AsteroidSeed,
    string Status,
    int CurrentGameDay,
    decimal Credits,
    IReadOnlyList<MineZoneDto> Zones,
    IReadOnlyList<WorkerDto> Workers,
    IReadOnlyList<InventoryItemDto> Inventory,
    FeatureFlags Features);

public record MarketPriceDto(SupplyTypeDto SupplyType, decimal Price, decimal ChangePct);
public record MarketTodayResponse(int GameDay, IReadOnlyList<MarketPriceDto> Prices, string Source);

public record FinanceResponse(
    decimal Credits,
    decimal DailyPayroll,
    decimal DailySupplyCost,
    decimal EstimatedDailyIncome,
    decimal RunwayDays,
    bool IsSoftlocked,
    bool CanEmergencyBuyback,
    IReadOnlyList<TransactionDto> RecentTransactions);

public record OreExtractedDto(string OreType, decimal Quantity);

public record DayAdvanceResponse(
    int NewGameDay,
    decimal Credits,
    IReadOnlyList<OreExtractedDto> OreExtracted,
    decimal PayrollPaid,
    decimal SuppliesConsumed,
    MarketTodayResponse Market,
    IReadOnlyList<string> Messages);

public record ActionResponse(bool Success, string Message, decimal? NewCredits = null);
