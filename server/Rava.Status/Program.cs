using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Rava.Status;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StatusMonitorOptions>(
    builder.Configuration.GetSection(StatusMonitorOptions.SectionName));
builder.Services.AddHttpClient("RavaApi");
builder.Services.AddHttpClient("RavaDocs");
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
    var client = httpClientFactory.CreateClient("RavaApi");
    client.Timeout = TimeSpan.FromSeconds(8);

    ApiStatusPayload? apiStatus = null;
    string? apiError = null;
    long? apiResponseMs = null;
    var apiReachable = false;

    try
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync($"{apiBaseUrl}/api/status", cancellationToken);
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

    var docsInternalUrl = monitor.DocsInternalUrl.TrimEnd('/');
    var docsClient = httpClientFactory.CreateClient("RavaDocs");
    docsClient.Timeout = TimeSpan.FromSeconds(8);

    string? docsError = null;
    long? docsResponseMs = null;
    var docsReachable = false;

    try
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await docsClient.GetAsync($"{docsInternalUrl}/", cancellationToken);
        stopwatch.Stop();
        docsResponseMs = stopwatch.ElapsedMilliseconds;
        docsReachable = response.IsSuccessStatusCode;

        if (!response.IsSuccessStatusCode)
        {
            docsError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }
    catch (Exception ex)
    {
        docsError = ex.Message;
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
        docsInternalUrl,
        monitor.DocsPublicUrl,
        apiReachable,
        apiResponseMs,
        apiError,
        apiStatus,
        docsReachable,
        docsResponseMs,
        docsError));
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
