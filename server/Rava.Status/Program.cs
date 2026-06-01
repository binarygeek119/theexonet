using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Status;

var contentRootPath = Path.GetFullPath(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath,
});

builder.Configuration.AddRavaDataJsonFiles(contentRootPath);

builder.Services.Configure<StatusMonitorOptions>(
    builder.Configuration.GetSection(StatusMonitorOptions.SectionName));
builder.Services.AddHttpClient("RavaApi");
builder.Services.AddHttpClient("RavaPortal");
builder.Services.AddSingleton<MonitorRuntimeInfo>();

var app = builder.Build();
var monitorRuntime = app.Services.GetRequiredService<MonitorRuntimeInfo>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/dashboard", async (
    IHttpClientFactory httpClientFactory,
    IOptions<StatusMonitorOptions> options,
    MonitorRuntimeInfo runtime,
    CancellationToken cancellationToken) =>
{
    var monitor = options.Value;
    var apiBaseUrl = monitor.ApiBaseUrl.TrimEnd('/');
    var apiClient = httpClientFactory.CreateClient("RavaApi");
    apiClient.Timeout = TimeSpan.FromSeconds(8);

    ApiStatusPayload? apiStatus = null;
    string? apiError = null;
    long? apiResponseMs = null;
    var apiReachable = false;

    try
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await apiClient.GetAsync($"{apiBaseUrl}/api/status", cancellationToken);
        stopwatch.Stop();
        apiResponseMs = stopwatch.ElapsedMilliseconds;
        apiReachable = response.IsSuccessStatusCode;

        if (response.IsSuccessStatusCode)
        {
            apiStatus = await response.Content.ReadFromJsonAsync<ApiStatusPayload>(cancellationToken);
        }
        else
        {
            apiError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }
    catch (Exception ex)
    {
        apiError = ex.Message;
    }

    var portalClient = httpClientFactory.CreateClient("RavaPortal");
    portalClient.Timeout = TimeSpan.FromSeconds(8);

    var docsPortal = await ProbePortalAsync(
        portalClient,
        monitor.DocsInternalUrl,
        monitor.DocsPublicUrl,
        "/",
        cancellationToken);
    var adminPortal = await ProbePortalAsync(
        portalClient,
        monitor.AdminInternalUrl,
        monitor.AdminPublicUrl,
        "/admin.html",
        cancellationToken);
    var moderatorPortal = await ProbePortalAsync(
        portalClient,
        monitor.ModeratorInternalUrl,
        monitor.ModeratorPublicUrl,
        "/moderator.html",
        cancellationToken);

    return Results.Ok(new DashboardResponse(
        DateTime.UtcNow,
        runtime.UptimeSeconds,
        runtime.StartedUtc,
        runtime.FirstRunUtc,
        apiBaseUrl,
        monitor.GameUrl,
        monitor.ApiPublicUrl,
        monitor.StatusPublicUrl,
        docsPortal.InternalUrl,
        docsPortal.PublicUrl,
        apiReachable,
        apiResponseMs,
        apiError,
        apiStatus,
        docsPortal,
        adminPortal,
        moderatorPortal));
});

app.MapGet("/api/economy", async (
    IHttpClientFactory httpClientFactory,
    IOptions<StatusMonitorOptions> options,
    CancellationToken cancellationToken) =>
{
    var apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
    var client = httpClientFactory.CreateClient("RavaApi");
    client.Timeout = TimeSpan.FromSeconds(12);

    using var response = await client.GetAsync($"{apiBaseUrl}/api/status/economy", cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            detail: $"API returned HTTP {(int)response.StatusCode}",
            statusCode: (int)response.StatusCode);
    }

    var payload = await response.Content.ReadFromJsonAsync<EconomyPayload>(cancellationToken);
    return payload is null
        ? Results.Problem("Could not read economy payload from API.")
        : Results.Ok(payload);
});

app.Logger.LogInformation("RAVA status dashboard listening on {Urls}", builder.Configuration["Urls"] ?? "http://0.0.0.0:6000");
app.Run();

static async Task<PortalStatusPayload> ProbePortalAsync(
    HttpClient client,
    string internalUrl,
    string publicUrl,
    string path,
    CancellationToken cancellationToken)
{
    internalUrl = internalUrl.TrimEnd('/');
    publicUrl = publicUrl.TrimEnd('/');
    var requestUrl = $"{internalUrl}{path}";

    try
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(requestUrl, cancellationToken);
        stopwatch.Stop();

        return new PortalStatusPayload(
            internalUrl,
            publicUrl,
            response.IsSuccessStatusCode,
            stopwatch.ElapsedMilliseconds,
            response.IsSuccessStatusCode
                ? null
                : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
    }
    catch (Exception ex)
    {
        return new PortalStatusPayload(internalUrl, publicUrl, false, null, ex.Message);
    }
}
