namespace Rava.Status;

public record ApiStatusPayload(
    string Status,
    string Service,
    DateTime Utc,
    bool DatabaseConnected,
    string DatabaseStatus);

public record DashboardResponse(
    DateTime Utc,
    double MonitorUptimeSeconds,
    string ApiBaseUrl,
    string GameUrl,
    string ApiPublicUrl,
    string StatusPublicUrl,
    bool ApiReachable,
    long? ApiResponseMs,
    string? ApiError,
    ApiStatusPayload? ApiStatus);
