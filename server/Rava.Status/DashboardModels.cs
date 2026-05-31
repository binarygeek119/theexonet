namespace Rava.Status;

public record ApiStatusPayload(
    string Status,
    string Service,
    DateTime Utc,
    bool DatabaseConnected,
    string DatabaseStatus,
    int? PlayerCount,
    double ServerUptimeSeconds,
    DateTime? ServerStartedUtc,
    DateTime? ServerFirstRunUtc,
    string GameVersion);

public record EconomyItemPayload(
    string ItemType,
    string Category,
    decimal Price,
    decimal? BasePrice,
    decimal? ChangePct,
    string? Note);

public record EconomyPayload(
    DateTime Utc,
    int ReferenceGameDay,
    string MarketSource,
    string MarketDate,
    decimal EmergencyBuybackRate,
    decimal SignUpCredits,
    decimal BirthdayBonusCredits,
    IReadOnlyList<EconomyItemPayload> OrePrices,
    IReadOnlyList<EconomyItemPayload> SupplyPrices);

public record DashboardResponse(
    DateTime Utc,
    double MonitorUptimeSeconds,
    DateTime MonitorStartedUtc,
    DateTime MonitorFirstRunUtc,
    string ApiBaseUrl,
    string GameUrl,
    string ApiPublicUrl,
    string StatusPublicUrl,
    bool ApiReachable,
    long? ApiResponseMs,
    string? ApiError,
    ApiStatusPayload? ApiStatus);
