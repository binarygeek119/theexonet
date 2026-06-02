using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
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
builder.Services.AddHttpClient("OpenAiStatus");
builder.Services.AddSingleton<MonitorRuntimeInfo>();

var app = builder.Build();
var monitorRuntime = app.Services.GetRequiredService<MonitorRuntimeInfo>();

var staticFiles = BuildStatusStaticFileProvider(contentRootPath, app.Environment);
app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = staticFiles,
    DefaultFileNames = ["index.html"],
});
app.UseStaticFiles(new StaticFileOptions { FileProvider = staticFiles });

app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg", permanent: false));

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

    var openAiClient = httpClientFactory.CreateClient("OpenAiStatus");
    openAiClient.Timeout = TimeSpan.FromSeconds(8);
    var openAiStatus = await OpenAiStatusProbe.FetchAsync(
        openAiClient,
        monitor.OpenAiStatusSummaryUrl,
        monitor.OpenAiStatusPageUrl,
        cancellationToken);

    OpenAiUsagePayload? openAiUsage = null;
    try
    {
        using var usageResponse = await apiClient.GetAsync($"{apiBaseUrl}/api/status/openai", cancellationToken);

        if (usageResponse.IsSuccessStatusCode)
        {
            var usage = await usageResponse.Content.ReadFromJsonAsync<PublicOpenAiStatusDetailResponse>(cancellationToken);
            if (usage is not null)
            {
                openAiUsage = new OpenAiUsagePayload(
                    usage.Utc,
                    usage.ApiKeyConfigured,
                    usage.TotalRequests,
                    usage.SuccessfulRequests,
                    usage.FailedRequests,
                    usage.RequestsToday,
                    usage.SuccessfulRequestsToday,
                    usage.FailedRequestsToday,
                    usage.RequestsByCategory,
                    usage.SuccessfulRequestsByCategory,
                    usage.FailedRequestsByCategory,
                    usage.LastRequestUtc,
                    usage.CreditsRemainingUsd,
                    usage.CreditsGrantedUsd,
                    usage.CreditsUsedUsd,
                    usage.CreditsNote,
                    true,
                    null);
            }
        }
        else
        {
            openAiUsage = new OpenAiUsagePayload(
                DateTime.UtcNow,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                new Dictionary<string, long>(),
                new Dictionary<string, long>(),
                new Dictionary<string, long>(),
                null,
                null,
                null,
                null,
                null,
                false,
                $"HTTP {(int)usageResponse.StatusCode} {usageResponse.ReasonPhrase}");
        }
    }
    catch (Exception ex)
    {
        openAiUsage = new OpenAiUsagePayload(
            DateTime.UtcNow,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            new Dictionary<string, long>(),
            new Dictionary<string, long>(),
            new Dictionary<string, long>(),
            null,
            null,
            null,
            null,
            null,
            false,
            ex.Message);
    }

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
        moderatorPortal,
        openAiStatus,
        openAiUsage));
});

app.MapGet("/api/openai", async (
    IHttpClientFactory httpClientFactory,
    IOptions<StatusMonitorOptions> options,
    CancellationToken cancellationToken) =>
{
    var monitor = options.Value;
    var apiBaseUrl = monitor.ApiBaseUrl.TrimEnd('/');
    var apiClient = httpClientFactory.CreateClient("RavaApi");
    apiClient.Timeout = TimeSpan.FromSeconds(12);

    PublicOpenAiStatusDetailResponse? rava = null;
    string? ravaError = null;
    try
    {
        using var response = await apiClient.GetAsync($"{apiBaseUrl}/api/status/openai", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            rava = await response.Content.ReadFromJsonAsync<PublicOpenAiStatusDetailResponse>(cancellationToken);
        }
        else
        {
            ravaError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }
    catch (Exception ex)
    {
        ravaError = ex.Message;
    }

    var openAiClient = httpClientFactory.CreateClient("OpenAiStatus");
    openAiClient.Timeout = TimeSpan.FromSeconds(10);
    var platform = await OpenAiStatusProbe.FetchAsync(
        openAiClient,
        monitor.OpenAiStatusSummaryUrl,
        monitor.OpenAiStatusPageUrl,
        cancellationToken);

    return Results.Ok(new OpenAiPageResponse(
        DateTime.UtcNow,
        monitor.GameUrl,
        monitor.ApiPublicUrl,
        platform,
        rava,
        ravaError));
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

static IFileProvider BuildStatusStaticFileProvider(string contentRootPath, IWebHostEnvironment environment)
{
    var providers = new List<IFileProvider>();

    var dedicatedStatusRoot = Path.Combine(contentRootPath, "status-wwwroot");
    if (Directory.Exists(dedicatedStatusRoot))
    {
        providers.Add(new PhysicalFileProvider(dedicatedStatusRoot));
    }

    var sharedWebRoot = Path.Combine(contentRootPath, "wwwroot");
    if (Directory.Exists(sharedWebRoot))
    {
        providers.Add(new PhysicalFileProvider(sharedWebRoot));
    }

    providers.Add(environment.WebRootFileProvider);

    return providers.Count == 1
        ? providers[0]
        : new CompositeFileProvider(providers);
}

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
