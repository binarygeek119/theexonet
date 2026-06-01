using Rava.Core.Enums;
using Rava.Core.Constants;

namespace Rava.Infrastructure.Entities;

public class PlayerEntity
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal Credits { get; set; }
    public int CurrentGameDay { get; set; } = 1;
    public DateOnly LastProcessedUtcDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? Birthday { get; set; }
    public int? LastBirthdayBonusYear { get; set; }
    public string ProfileMood { get; set; } = PlayerProfileDefaults.Mood;
    public string ProfileAboutMe { get; set; } = PlayerProfileDefaults.AboutMe;
    public string ProfileTheme { get; set; } = PlayerProfileDefaults.Theme;
    public string ProfileMusic { get; set; } = PlayerProfileDefaults.Music;
    public string ProfileInterests { get; set; } = PlayerProfileDefaults.Interests;
    public string ProfileDiscord { get; set; } = string.Empty;
    public string ProfileBluesky { get; set; } = string.Empty;
    public string ProfileTwitter { get; set; } = string.Empty;
    public string ProfileYoutube { get; set; } = string.Empty;
    public string ProfileFacebook { get; set; } = string.Empty;
    public string ProfileNumber { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public int ProfileImageRevision { get; set; }
    public string ProfileBackgroundUrl { get; set; } = string.Empty;
    public int ProfileBackgroundRevision { get; set; }

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
    public DateOnly? UtcDate { get; set; }
    public SupplyType SupplyType { get; set; }
    public decimal Price { get; set; }
    public decimal ChangePct { get; set; }
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
    public PlayerEntity Friend { get; set; } = null!;
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

public class CompanyNameLimboEntity
{
    public Guid Id { get; set; }
    public Guid? PlayerId { get; set; }
    public string NormalizedName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime AvailableAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity? Player { get; set; }
}

public class CompanyNameListingEntity
{
    public Guid Id { get; set; }
    public Guid SellerPlayerId { get; set; }
    public Guid SellerMineId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = CompanyNameListingStatuses.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt { get; set; }
    public Guid? BuyerPlayerId { get; set; }

    public PlayerEntity Seller { get; set; } = null!;
}

public class AccountResetEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public DateTime ResetAt { get; set; }
}

public class PasswordResetTokenEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Used { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class ProfileFlagEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string FlaggedByUsername { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class PlayerBanEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string BanLevel { get; set; } = string.Empty;
    public string BannedByUsername { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LiftedAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class BanAppealEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? BanId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = BanAppealStatuses.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewedByUsername { get; set; } = string.Empty;

    public PlayerEntity Player { get; set; } = null!;
}

public class StaffMessageEntity
{
    public Guid Id { get; set; }
    public string FromUsername { get; set; } = string.Empty;
    public string ToUsername { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public class PlayerMessageEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string FromStaffUsername { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class PeerMessageEntity
{
    public Guid Id { get; set; }
    public Guid FromPlayerId { get; set; }
    public Guid ToPlayerId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public PlayerEntity FromPlayer { get; set; } = null!;
    public PlayerEntity ToPlayer { get; set; } = null!;
}

public class PlayerToStaffMessageEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string ToStaffUsername { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}

public class FlaggedMessageEntity
{
    public Guid Id { get; set; }
    public Guid? PlayerId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public Guid SourceMessageId { get; set; }
    public string FromLabel { get; set; } = string.Empty;
    public string ToLabel { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string MatchedTerms { get; set; } = string.Empty;
    public string Status { get; set; } = FlaggedMessageStatuses.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string ReviewedByUsername { get; set; } = string.Empty;

    public PlayerEntity? Player { get; set; }
}

public class PlayerWarningEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? FlaggedMessageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IssuedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
    public FlaggedMessageEntity? FlaggedMessage { get; set; }
}

public class SpecialEventEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string ChallengeType { get; set; } = SpecialEventChallengeType.AdvanceDay.ToString();
    public int ChallengeTarget { get; set; } = 1;
    public string ChallengeDetail { get; set; } = string.Empty;
    public decimal SaleBonusPercent { get; set; }
    public decimal TradeBonusPercent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SpecialEventRewardEntity> Rewards { get; set; } = [];
    public ICollection<SpecialEventClaimEntity> Claims { get; set; } = [];
    public ICollection<SpecialEventProgressEntity> ProgressEntries { get; set; } = [];
    public ICollection<SpecialEventAnnouncementEntity> Announcements { get; set; } = [];
}

public class SpecialEventRewardEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public SpecialEventEntity Event { get; set; } = null!;
}

public class SpecialEventClaimEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid EventId { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
    public SpecialEventEntity Event { get; set; } = null!;
}

public class SpecialEventProgressEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid EventId { get; set; }
    public int ProgressCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
    public SpecialEventEntity Event { get; set; } = null!;
}

public class SpecialEventAnnouncementEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Guid EventId { get; set; }
    public DateTime AnnouncedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
    public SpecialEventEntity Event { get; set; } = null!;
}
