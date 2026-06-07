using System;
using Theexonet.Core.Enums;

namespace Theexonet.Core.Dtos
{
[Serializable]
public class RegisterRequest
{
    public string username;
    public string email;
    public string password;
    public string birthday;
}

[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class ForgotPasswordRequest
{
    public string email;
}

[Serializable]
public class ResetPasswordRequest
{
    public string token;
    public string newPassword;
}

[Serializable]
public class MessageResponse
{
    public string message;
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
    public string utcDate;
    public string nextDayAtUtc;
    public DayAdvanceResponse latestDayReport;
    public string birthdayMessage;
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
public class UpdatePlayerProfileRequest
{
    public string mood;
    public string aboutMe;
    public string music;
    public string interests;
}

[Serializable]
public class PlayerProfileResponse
{
    public string playerId;
    public string username;
    public string profileNumber;
    public string profileImageUrl;
    public string mood;
    public string aboutMe;
    public string music;
    public string interests;
    public string memberSince;
    public int currentGameDay;
    public float credits;
    public string mineName;
    public int workerCount;
    public int zoneCount;
    public bool isOwner;
    public string friendshipStatus;
    public string friendshipId;
}

[Serializable]
public class ErrorResponse
{
    public string message;
}

[Serializable]
public class FriendSummaryDto
{
    public string friendshipId;
    public string playerId;
    public string username;
    public string profileNumber;
    public string mood;
    public string status;
    public string since;
}

[Serializable]
public class FriendsListResponse
{
    public FriendSummaryDto[] friends;
    public FriendSummaryDto[] incomingRequests;
    public FriendSummaryDto[] outgoingRequests;
}

[Serializable]
public class AddFriendRequest
{
    public string profileNumber;
}

[Serializable]
public class FriendActionResponse
{
    public bool success;
    public string message;
}
}
