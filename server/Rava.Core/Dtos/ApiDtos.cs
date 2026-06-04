using Rava.Core.Constants;
using Rava.Core.Models;

namespace Rava.Core.Dtos;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string Birthday,
    string ProfileGender = "",
    string? ProfilePreferredPronouns = null,
    string ProfileLocale = "",
    bool ProfileBirthdayPublic = false,
    bool ProfileAgePublic = false);
public record LoginRequest(string Username, string Password);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record MessageResponse(string Message);
public record ApiStatusResponse(
    string Status,
    string Service,
    DateTime Utc,
    bool DatabaseConnected,
    string DatabaseStatus,
    int? PlayerCount = null,
    double ServerUptimeSeconds = 0,
    DateTime? ServerStartedUtc = null,
    DateTime? ServerFirstRunUtc = null,
    string GameVersion = "");

public record PublicOpenAiUsageResponse(
    DateTime Utc,
    bool ApiKeyConfigured,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long RequestsToday,
    long SuccessfulRequestsToday,
    long FailedRequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    IReadOnlyDictionary<string, long> SuccessfulRequestsByCategory,
    IReadOnlyDictionary<string, long> FailedRequestsByCategory,
    DateTime? LastRequestUtc,
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    decimal? CreditsUsedUsd,
    string? CreditsNote);

public record PublicOpenAiGameFeatureDto(
    string Id,
    string Title,
    string Description,
    string RequestCategory,
    bool Enabled,
    string? ModelOrEndpoint);

public record PublicOpenAiExonetSnapshotDto(
    bool OffworldNewsEnabled,
    DateOnly? TodayEditionDate,
    string? TodayEditionSource,
    int? TodayStoryCount,
    int? TodayIllustratedStories,
    int ReporterPoolSize,
    int ActiveReporterPool,
    int TotalReporters,
    int ArchivedEditionCount,
    string PortraitJobStatus,
    string? PortraitJobMessage,
    int PortraitJobImagesSaved,
    int PortraitJobImageAttempts);

public record PublicOpenAiConfigurationDto(
    bool OffworldNewsEnabled,
    bool CompanyLogoEnabled,
    bool ApiKeyConfigured,
    string? ApiKeyHint,
    string BaseUrl,
    string TextModel,
    string ImageModel,
    int StoriesPerDay,
    int MaxImagesPerDay,
    bool CompanyLogoUsesDedicatedKey,
    string CompanyLogoImageModel,
    int CompanyLogoSecondsBetweenGenerations,
    string CompanyLogoBaseUrl);

public record PublicOpenAiStatusDetailResponse(
    DateTime Utc,
    bool ApiKeyConfigured,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long RequestsToday,
    long SuccessfulRequestsToday,
    long FailedRequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    IReadOnlyDictionary<string, long> SuccessfulRequestsByCategory,
    IReadOnlyDictionary<string, long> FailedRequestsByCategory,
    DateTime? LastRequestUtc,
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    decimal? CreditsUsedUsd,
    string? CreditsNote,
    PublicOpenAiConfigurationDto Configuration,
    IReadOnlyList<PublicOpenAiGameFeatureDto> GameFeatures,
    PublicOpenAiExonetSnapshotDto Exonet);

public record EconomyItemPriceDto(
    string ItemType,
    string Category,
    decimal Price,
    decimal? BasePrice,
    decimal? ChangePct,
    string? Note = null);

public record PublicEconomyResponse(
    DateTime Utc,
    int ReferenceGameDay,
    string MarketSource,
    string MarketDate,
    decimal EmergencyBuybackRate,
    decimal SignUpCredits,
    decimal BirthdayBonusCredits,
    decimal TradeMarketValue,
    decimal AuctionFeePercent,
    IReadOnlyList<EconomyItemPriceDto> OrePrices,
    IReadOnlyList<EconomyItemPriceDto> SupplyPrices);

public record TradeItemDto(
    string ItemType,
    string Category,
    decimal BasePrice,
    string DisplayName,
    string Color,
    string? UiSymbol,
    bool IsEmergencySource);

public record TradeItemsResponse(IReadOnlyList<TradeItemDto> Items);

public record TradeMarketInfoResponse(
    decimal TradeMarketValue,
    decimal AuctionFeePercent,
    int MinAuctionDurationMinutes,
    int MaxAuctionDurationMinutes);

public record CreateTradeAuctionRequest(
    string Category,
    string ItemType,
    decimal Quantity,
    decimal StartPrice,
    int DurationMinutes);

public record PlaceTradeAuctionBidRequest(decimal BidAmount);

public record TradeAuctionDto(
    Guid Id,
    string SellerUsername,
    string Category,
    string ItemType,
    string DisplayName,
    decimal Quantity,
    decimal StartPrice,
    decimal? CurrentBid,
    string? HighBidderUsername,
    int DurationMinutes,
    DateTime? EndsAt,
    string Status,
    bool IsMine,
    decimal MinimumNextBid,
    int? SecondsRemaining);

public record TradeAuctionListResponse(
    decimal TradeMarketValue,
    decimal AuctionFeePercent,
    IReadOnlyList<TradeAuctionDto> Auctions);

public record TradeAuctionActionResponse(bool Success, string Message, decimal? NewCredits = null);
public record AuthResponse(
    string Token,
    Guid PlayerId,
    Guid MineId,
    string Username,
    FeatureFlags Features,
    IReadOnlyList<LoginEventAnnouncementDto>? EventAnnouncements = null,
    bool IsStaffAdmin = false,
    bool TestingModeEnabled = false,
    IReadOnlyList<PlayerModerationWarningDto>? PendingWarnings = null);

public record SessionResponse(
    Guid PlayerId,
    Guid MineId,
    string Username,
    IReadOnlyList<LoginEventAnnouncementDto>? EventAnnouncements = null,
    bool IsStaffAdmin = false,
    bool TestingModeEnabled = false);

public record PlayerModerationWarningDto(
    Guid Id,
    string Reason,
    string IssuedByUsername,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public record AcknowledgeWarningResponse(
    int RemainingCount,
    IReadOnlyList<PlayerModerationWarningDto> RemainingWarnings);

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
    FeatureFlags Features,
    string UtcDate,
    DateTime NextDayAtUtc,
    DayAdvanceResponse? LatestDayReport = null,
    string? BirthdayMessage = null,
    IReadOnlyList<EventCompletionDto>? EventCompletions = null);

public record MarketPriceDto(SupplyTypeDto SupplyType, decimal Price, decimal ChangePct);

public record ActiveMarketBonusesDto(
    decimal SaleBonusPercent,
    decimal TradeBonusPercent,
    IReadOnlyList<string> EventTitles);

public record MarketTodayResponse(
    int GameDay,
    IReadOnlyList<MarketPriceDto> Prices,
    string Source,
    ActiveMarketBonusesDto? EventBonuses = null);

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
    IReadOnlyList<string> Messages,
    IReadOnlyList<EventCompletionDto>? EventCompletions = null);

public record ActionResponse(
    bool Success,
    string Message,
    decimal? NewCredits = null,
    IReadOnlyList<EventCompletionDto>? EventCompletions = null);

public record PlayerProfileResponse(
    Guid PlayerId,
    string Username,
    string ProfileNumber,
    string ProfileImageUrl,
    string ProfileBackgroundUrl,
    string CompanyLogoUrl,
    string Mood,
    string AboutMe,
    string Music,
    string Interests,
    string Discord,
    string Bluesky,
    string Twitter,
    string Youtube,
    string Facebook,
    DateTime MemberSince,
    int CurrentGameDay,
    decimal Credits,
    string MineName,
    int WorkerCount,
    int ZoneCount,
    bool IsOwner,
    string FriendshipStatus,
    string FriendshipId,
    ProfileFlagDto? ActiveFlag = null,
    IReadOnlyList<ProfileFriendDto>? Friends = null,
    Guid? MineId = null,
    bool CompanyNameListed = false,
    Guid? CompanyNameListingId = null,
    decimal? CompanyNameListingPrice = null,
    IReadOnlyList<ReclaimableCompanyNameDto>? ReclaimableCompanyNames = null,
    decimal CompanyNameReclaimFee = 0,
    bool IsReporter = false,
    string ReporterSlug = "",
    string OnnProfilePath = "",
    string CompanyLogoGenerationStatus = "none",
    string CompanyLogoGenerationMessage = "",
    bool CompanyLogoAiEnabled = false,
    string ProfileAvatarPreset = ProfileAvatarPresets.DefaultPreset,
    bool HasCustomProfilePhoto = false,
    string ProfileGender = "",
    string ProfilePreferredPronouns = "",
    string ProfileLocale = "",
    string PronounSubject = "they",
    string PronounObject = "them",
    string PronounPossessive = "their",
    string PronounLabel = "they/them",
    bool RequiresPreferredPronouns = false,
    bool ProfileCompletionRequired = false,
    IReadOnlyList<ProfileCompletionFieldDto>? MissingProfileFields = null,
    string ReportedLocationsNote = "",
    bool ProfileBirthdayPublic = false,
    bool ProfileAgePublic = false,
    string? PublicBirthday = null,
    int? PublicAge = null,
    bool IsStaffAdmin = false,
    bool TestingModeEnabled = false);

public record ProfileCompletionFieldDto(string FieldId);

public record UpdateCompanyNameRequest(string CompanyName);

public record ListCompanyNameRequest(decimal Price);

public record CompanyNameActionResponse(
    string CompanyName,
    Guid MineId,
    bool CompanyNameListed,
    Guid? CompanyNameListingId,
    decimal? CompanyNameListingPrice,
    string Message,
    IReadOnlyList<ReclaimableCompanyNameDto>? ReclaimableCompanyNames = null,
    decimal CompanyNameReclaimFee = 0);

public record CompanyNameListingDto(
    Guid Id,
    string CompanyName,
    string SellerUsername,
    decimal Price,
    DateTime ListedAt);

public record CompanyNameListingsResponse(IReadOnlyList<CompanyNameListingDto> Listings);

public record PublicProfileSummaryDto(
    string Username,
    string ProfileNumber,
    string CompanyName,
    string CompanyLogoUrl,
    string Mood,
    string ProfileImageUrl,
    int CurrentGameDay,
    int WorkerCount,
    int ZoneCount,
    decimal CompanyValue,
    int Rank = 0,
    bool IsReporter = false,
    string ReporterSlug = "",
    DateTime? MemberSince = null,
    bool IsOnline = false,
    bool BirthdayToday = false,
    string? PublicBirthday = null,
    int? PublicAge = null);

public record PublicProfileBrowseResponse(
    string Sort,
    int TotalCount,
    int Offset,
    int Limit,
    IReadOnlyList<PublicProfileSummaryDto> Entries,
    IReadOnlyList<string> AvailableSorts);

public record PublicProfileDetailDto(
    string Username,
    string ProfileNumber,
    string CompanyName,
    string CompanyLogoUrl,
    string ProfileImageUrl,
    string Mood,
    string AboutMe,
    string Interests,
    string Music,
    string Discord,
    string Bluesky,
    string Twitter,
    string Youtube,
    string Facebook,
    DateTime MemberSince,
    int CurrentGameDay,
    int WorkerCount,
    int ZoneCount,
    decimal CompanyValue,
    bool IsReporter = false,
    string ReporterSlug = "",
    string OnnProfilePath = "",
    string PronounSubject = "they",
    string PronounObject = "them",
    string PronounPossessive = "their",
    string PronounLabel = "they/them",
    string ReportedLocationsNote = "",
    string? PublicBirthday = null,
    int? PublicAge = null);

public record PublicProfileSearchResponse(
    string Query,
    string Mode,
    IReadOnlyList<PublicProfileSummaryDto> Results);

public record PublicProfileLeaderboardResponse(
    string Sort,
    IReadOnlyList<PublicProfileSummaryDto> Entries,
    IReadOnlyList<string> ComingSoonSorts);

public record ProfileFriendDto(
    Guid PlayerId,
    string Username,
    string ProfileNumber,
    string Mood,
    string PublicStatus,
    bool IsReporter = false,
    string ReporterSlug = "",
    bool IsTestingDummy = false);

public record UpdatePlayerProfileRequest(
    string Mood,
    string AboutMe,
    string Music,
    string Interests,
    string Discord,
    string Bluesky,
    string Twitter,
    string Youtube,
    string Facebook,
    string? ProfileAvatarPreset = null,
    string? ProfileGender = null,
    string? ProfilePreferredPronouns = null,
    string? ProfileLocale = null,
    bool? ProfileBirthdayPublic = null,
    bool? ProfileAgePublic = null);

public record FriendSummaryDto(
    Guid FriendshipId,
    Guid PlayerId,
    string Username,
    string ProfileNumber,
    string Mood,
    string Status,
    DateTime Since,
    bool IsReporter = false,
    string ReporterSlug = "",
    bool IsTestingDummy = false);

public record FriendsListResponse(
    IReadOnlyList<FriendSummaryDto> Friends,
    IReadOnlyList<FriendSummaryDto> IncomingRequests,
    IReadOnlyList<FriendSummaryDto> OutgoingRequests);

public record AddFriendRequest(string ProfileNumber);

public record FriendActionResponse(bool Success, string Message);

public record AdminMeResponse(string Username, bool IsAdmin, bool TestingModeEnabled = false);

public record AdminTestingModeRequest(bool Enabled);

public record AdminTestingModeResponse(bool Enabled);

public record TestingDummyAssetsEnsureResponse(
    bool Started,
    bool AlreadyRunning,
    int MissingAssets,
    string Message);

public record AdminTestingDummyActionRequest(int DummyIndex);

public record AdminTestingActionResponse(
    string Message,
    Guid? ResourceId = null,
    string? Channel = null);

public record ModeratorMeResponse(string Username, bool IsModerator, bool IsAdmin);

public record AdminDashboardResponse(
    int PlayerCount,
    int MineCount,
    int FriendshipCount,
    int CurrentGameDay,
    decimal TotalCredits,
    decimal SignUpCredits,
    decimal BirthdayBonus);

public record AdminPlayerSummary(
    Guid Id,
    string Username,
    string Email,
    decimal Credits,
    DateTime CreatedAt,
    int MineCount,
    PlayerBanDto? ActiveBan = null);

public record AdminPlayersResponse(IReadOnlyList<AdminPlayerSummary> Players);

public record AdminSetCreditsRequest(decimal Credits);

public record GameCreditsConfigDto(decimal SignUp, decimal BirthdayBonus, decimal CompanyNameReclaimFee);

public record GameCreditsConfigResponse(GameCreditsConfigDto Credits, string FilePath);

public record UpdateGameCreditsConfigRequest(
    decimal SignUp,
    decimal BirthdayBonus,
    decimal CompanyNameReclaimFee);

public record UpdateGameCreditsConfigResponse(GameCreditsConfigDto Credits, string Message);

public record ReclaimableCompanyNameDto(
    string DisplayName,
    DateTime ReservedUntil,
    decimal ReclaimFee);

public record ReclaimCompanyNameRequest(string CompanyName);

public record AdminPlayerProfileResponse(
    Guid Id,
    string Username,
    string Email,
    string ProfileNumber,
    string ProfileImageUrl,
    string Mood,
    string AboutMe,
    string Music,
    string Interests,
    string Discord,
    string Bluesky,
    string Twitter,
    string Youtube,
    string Facebook,
    string Theme,
    DateTime MemberSince,
    string? Birthday,
    int? LastBirthdayBonusYear,
    int CurrentGameDay,
    decimal Credits,
    string MineName,
    int WorkerCount,
    int ZoneCount,
    int MineCount,
    ProfileFlagDto? ActiveFlag = null,
    IReadOnlyList<ProfileFlagDto>? FlagHistory = null,
    PlayerBanDto? ActiveBan = null,
    IReadOnlyList<PlayerBanDto>? BanHistory = null,
    bool IsProtectedAdmin = false,
    bool IsModerator = false,
    int WarningCount = 0,
    IReadOnlyList<PlayerWarningDto>? WarningHistory = null);

public record PlayerWarningDto(
    Guid Id,
    Guid? FlaggedMessageId,
    string Reason,
    string IssuedByUsername,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsActive,
    bool IsAcknowledged);

public record IssuePlayerWarningRequest(string Reason);

public record PlayerWarningResponse(PlayerWarningDto Warning, string Message);

public record FlaggedMessageReviewDto(
    Guid Id,
    Guid? PlayerId,
    string PlayerUsername,
    string Channel,
    Guid SourceMessageId,
    string FromLabel,
    string ToLabel,
    string Body,
    string MatchedTerms,
    string Status,
    DateTime CreatedAt,
    string? ReviewedByUsername,
    DateTime? ReviewedAt,
    int PlayerWarningCount,
    IReadOnlyList<PlayerWarningDto> PlayerWarnings,
    DateTime SentAt,
    bool SourceMessageDeleted);

public record FlaggedMessagesResponse(IReadOnlyList<FlaggedMessageReviewDto> Messages);

public record FlaggedMessagePendingCountResponse(int Count);

public record FlaggedMessageWarningResponse(
    PlayerWarningDto Warning,
    FlaggedMessageReviewDto Review,
    string Message);

public record FlaggedMessageBanResponse(
    PlayerBanDto Ban,
    FlaggedMessageReviewDto Review,
    string Message);

public record BanLevelOptionDto(string Code, string Label);

public record BanReasonPresetsResponse(IReadOnlyList<string> Presets);

public record PlayerBanDto(
    Guid Id,
    string BanLevel,
    string BanLevelLabel,
    string BannedByUsername,
    string? Reason,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    bool IsPermanent,
    bool IsActive,
    DateTime? LiftedAt);

public record SetPlayerBanRequest(string BanLevel, string? Reason);

public record PlayerBanActionResponse(PlayerBanDto? Ban, string Message);

public record BanAppealRequest(string Username, string Password, string Message);

public record BanAppealDto(
    Guid Id,
    Guid PlayerId,
    string Username,
    string Email,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByUsername,
    PlayerBanDto? ActiveBan);

public record BanAppealsResponse(IReadOnlyList<BanAppealDto> Appeals);

public record AdminBanListItemDto(
    Guid BanId,
    Guid PlayerId,
    string Username,
    string Email,
    string ProfileNumber,
    PlayerBanDto Ban);

public record AdminBansResponse(IReadOnlyList<AdminBanListItemDto> Bans);

public record StaffMemberDto(string Username, bool IsAdmin, bool IsModerator);

public record StaffMembersResponse(IReadOnlyList<StaffMemberDto> Members);

public record StaffMessageDto(
    Guid Id,
    string FromUsername,
    string ToUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record StaffMessagesResponse(IReadOnlyList<StaffMessageDto> Messages);

public record StaffUnreadCountResponse(int Count, int PlayerMessageCount);

public record SendStaffMessageRequest(string ToUsername, string Body);

public record SendStaffMessageResponse(StaffMessageDto Message, string StatusMessage);

public record PlayerMessageDto(
    Guid Id,
    string FromStaffUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record PlayerMessagesResponse(IReadOnlyList<PlayerMessageDto> Messages);

public record PlayerUnreadCountResponse(int Count);

public record SendPlayerMessageRequest(string Body);

public record SendPlayerMessageResponse(PlayerMessageDto Message, string StatusMessage);

public record PeerMessageDto(
    Guid Id,
    Guid FromPlayerId,
    string FromUsername,
    Guid ToPlayerId,
    string ToUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead,
    bool IsSentByMe);

public record PeerMessagesResponse(IReadOnlyList<PeerMessageDto> Messages);

public record SendPeerMessageRequest(Guid ToPlayerId, string Body);

public record SendPeerMessageResponse(PeerMessageDto Message, string StatusMessage);

public record StaffContactDto(string Username, bool IsAdmin, bool IsModerator);

public record StaffContactsResponse(IReadOnlyList<StaffContactDto> Contacts);

public record PlayerToStaffMessageDto(
    Guid Id,
    string ToStaffUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record PlayerToStaffMessagesResponse(IReadOnlyList<PlayerToStaffMessageDto> Messages);

public record SendPlayerToStaffMessageRequest(string ToStaffUsername, string Body);

public record SendPlayerToStaffMessageResponse(PlayerToStaffMessageDto Message, string StatusMessage);

public record PlayerToStaffInboxDto(
    Guid Id,
    Guid PlayerId,
    string PlayerUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record PlayerToStaffInboxResponse(IReadOnlyList<PlayerToStaffInboxDto> Messages);

public record MessageLogEntryDto(
    Guid Id,
    string Channel,
    string FromLabel,
    string ToLabel,
    string Body,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record MessageLogResponse(IReadOnlyList<MessageLogEntryDto> Entries);

public record ProfileFlagRequest(string Comment);

public record ProfileFlagDto(
    Guid Id,
    string FlaggedByUsername,
    string Comment,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

public record ProfileFlagResponse(
    ProfileFlagDto Flag,
    string Message);

public record EventRewardDto(string ItemType, decimal Amount);

public record SpecialEventDto(
    Guid Id,
    string Title,
    string Message,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string ChallengeType,
    int ChallengeTarget,
    string ChallengeDetail,
    string ChallengeDescription,
    decimal SaleBonusPercent,
    decimal TradeBonusPercent,
    string? MarketBonusDescription,
    IReadOnlyList<EventRewardDto> Rewards,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ClaimCount);

public record SaveSpecialEventRequest(
    string Title,
    string Message,
    bool IsActive,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string ChallengeType,
    int ChallengeTarget,
    string? ChallengeDetail,
    decimal SaleBonusPercent,
    decimal TradeBonusPercent,
    IReadOnlyList<EventRewardDto> Rewards);

public record SpecialEventsListResponse(IReadOnlyList<SpecialEventDto> Events);

public record EventRewardGrantDto(string ItemType, string Category, decimal Amount);

public record LoginEventAnnouncementDto(
    Guid EventId,
    string Title,
    string Message,
    string ChallengeType,
    int ChallengeTarget,
    string ChallengeDetail,
    string ChallengeDescription,
    decimal SaleBonusPercent,
    decimal TradeBonusPercent,
    string? MarketBonusDescription,
    IReadOnlyList<EventRewardDto> Rewards);

public record EventCompletionDto(
    Guid EventId,
    string Title,
    string Message,
    IReadOnlyList<EventRewardGrantDto> Rewards);
