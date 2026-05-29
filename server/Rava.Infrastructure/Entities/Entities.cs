using Rava.Core.Enums;

namespace Rava.Infrastructure.Entities;

public class PlayerEntity
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal Credits { get; set; }
    public int CurrentGameDay { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MineEntity> Mines { get; set; } = [];
    public ICollection<InventoryItemEntity> Inventory { get; set; } = [];
    public ICollection<TransactionEntity> Transactions { get; set; } = [];
}

public class MineEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AsteroidSeed { get; set; }
    public MineStatus Status { get; set; } = MineStatus.Active;
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
    public ICollection<MineZoneEntity> Zones { get; set; } = [];
    public ICollection<WorkerEntity> Workers { get; set; } = [];
}

public class MineZoneEntity
{
    public Guid Id { get; set; }
    public Guid MineId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public OreType OreType { get; set; }
    public decimal Richness { get; set; }
    public decimal DepletedPct { get; set; }
    public bool IsSalvageZone { get; set; }

    public MineEntity Mine { get; set; } = null!;
}

public class WorkerEntity
{
    public Guid Id { get; set; }
    public Guid MineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Skill { get; set; }
    public decimal Salary { get; set; }
    public Guid? AssignedZoneId { get; set; }

    public MineEntity Mine { get; set; } = null!;
}

public class InventoryItemEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public ItemCategory Category { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class TransactionEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int GameDay { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
}

public class MarketPriceHistoryEntity
{
    public Guid Id { get; set; }
    public int GameDay { get; set; }
    public SupplyType SupplyType { get; set; }
    public decimal Price { get; set; }
    public string Source { get; set; } = "mock";
}

public class GameWorldEntity
{
    public int Id { get; set; } = 1;
    public int CurrentDay { get; set; } = 1;
    public DateTime LastTickAt { get; set; } = DateTime.UtcNow;
    public int MarketSeed { get; set; } = 42;
}

// Reserved for future phases
public class FriendshipEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid FriendId { get; set; }
    public string Status { get; set; } = "pending";
}

public class MineGroupEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}

public class MarketListingEntity
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class AccountResetEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public DateTime ResetAt { get; set; }
}
