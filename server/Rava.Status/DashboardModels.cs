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

public record PortalStatusPayload(
    string InternalUrl,
    string PublicUrl,
    bool Reachable,
    long? ResponseMs,
    string? Error);

public record OpenAiComponentStatusPayload(string Name, string Status);

public record OpenAiStatusPayload(
    string StatusPageUrl,
    string SummaryUrl,
    bool Reachable,
    long? ResponseMs,
    string? Indicator,
    string? Description,
    IReadOnlyList<OpenAiComponentStatusPayload> DegradedComponents,
    string? Error);

public record OpenAiUsagePayload(
    DateTime Utc,
    bool ApiKeyConfigured,
    long TotalRequests,
    long RequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    DateTime? LastRequestUtc,
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    string? CreditsNote,
    bool Reachable,
    string? Error);

public record OpenAiUsageApiPayload(
    DateTime Utc,
    bool ApiKeyConfigured,
    long TotalRequests,
    long RequestsToday,
    IReadOnlyDictionary<string, long> RequestsByCategory,
    DateTime? LastRequestUtc,
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    string? CreditsNote);

public record DashboardResponse(
    DateTime Utc,
    double MonitorUptimeSeconds,
    DateTime MonitorStartedUtc,
    DateTime MonitorFirstRunUtc,
    string ApiBaseUrl,
    string GameUrl,
    string ApiPublicUrl,
    string StatusPublicUrl,
    string DocsInternalUrl,
    string DocsPublicUrl,
    bool ApiReachable,
    long? ApiResponseMs,
    string? ApiError,
    ApiStatusPayload? ApiStatus,
    PortalStatusPayload DocsPortal,
    PortalStatusPayload AdminPortal,
    PortalStatusPayload ModeratorPortal,
    OpenAiStatusPayload OpenAi,
    OpenAiUsagePayload? OpenAiUsage);
