using System;
using Rava.Core.Enums;

namespace Rava.Core.Dtos;

[Serializable]
public class RegisterRequest
{
    public string username;
    public string email;
    public string password;
}

[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class FeatureFlagsDto
{
    public bool trading;
    public bool friends;
    public bool multiMine;
    public bool mineGroups;
    public bool specialDeals;
    public bool accountNuke;
}

[Serializable]
public class AuthResponse
{
    public string token;
    public string playerId;
    public string mineId;
    public string username;
    public FeatureFlagsDto features;
}

[Serializable]
public class AssignWorkerRequest
{
    public string workerId;
    public string zoneId;
}

[Serializable]
public class BuySupplyRequest
{
    public SupplyTypeDto supplyType;
    public float quantity;
}

[Serializable]
public class SellOreRequest
{
    public OreTypeDto oreType;
    public float quantity;
    public bool emergencyBuyback;
}

[Serializable]
public class MineZoneDto
{
    public string id;
    public int x;
    public int y;
    public OreTypeDto oreType;
    public float richness;
    public float depletedPct;
    public bool isSalvageZone;
}

[Serializable]
public class WorkerDto
{
    public string id;
    public string name;
    public int skill;
    public float salary;
    public string assignedZoneId;
}

[Serializable]
public class InventoryItemDto
{
    public string itemType;
    public string category;
    public float quantity;
}

[Serializable]
public class MineDetailResponse
{
    public string id;
    public string name;
    public int asteroidSeed;
    public string status;
    public int currentGameDay;
    public float credits;
    public MineZoneDto[] zones;
    public WorkerDto[] workers;
    public InventoryItemDto[] inventory;
    public FeatureFlagsDto features;
}

[Serializable]
public class MarketPriceDto
{
    public SupplyTypeDto supplyType;
    public float price;
    public float changePct;
}

[Serializable]
public class MarketTodayResponse
{
    public int gameDay;
    public MarketPriceDto[] prices;
    public string source;
}

[Serializable]
public class TransactionDto
{
    public string type;
    public float amount;
    public string description;
    public int gameDay;
    public string createdAt;
}

[Serializable]
public class FinanceResponse
{
    public float credits;
    public float dailyPayroll;
    public float dailySupplyCost;
    public float estimatedDailyIncome;
    public float runwayDays;
    public bool isSoftlocked;
    public bool canEmergencyBuyback;
    public TransactionDto[] recentTransactions;
}

[Serializable]
public class DayAdvanceResponse
{
    public int newGameDay;
    public float credits;
    public OreExtractedEntry[] oreExtracted;
    public float payrollPaid;
    public float suppliesConsumed;
    public MarketTodayResponse market;
    public string[] messages;
}

[Serializable]
public class OreExtractedEntry
{
    public string oreType;
    public float quantity;
}

[Serializable]
public class ActionResponse
{
    public bool success;
    public string message;
    public float? newCredits;
}

[Serializable]
public class ErrorResponse
{
    public string message;
}
