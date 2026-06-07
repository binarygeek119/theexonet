using Theexonet.Core.Dtos;

namespace Theexonet.Status;

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
    IReadOnlyList<OpenAiComponentStatusPayload> AllComponents,
    string? Error);

public record OpenAiPageResponse(
    DateTime Utc,
    string GameUrl,
    string ApiPublicUrl,
    OpenAiStatusPayload Platform,
    PublicOpenAiStatusDetailResponse? Theexonet,
    string? TheexonetError);

public record OpenAiUsagePayload(
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
    bool Reachable,
    string? Error);

public record AiGenerationQueueStatusPayload(
    DateTime Utc,
    bool Enabled,
    string Status,
    string? CurrentJobDescription,
    string? CurrentJobKind,
    int QueuedCount,
    int CompletedToday,
    int FailedToday,
    IReadOnlyDictionary<string, int> QueuedByKind,
    bool Reachable,
    string? Error,
    string ApiPublicUrl);

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
