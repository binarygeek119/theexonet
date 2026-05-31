using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Rava.Status;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StatusMonitorOptions>(
    builder.Configuration.GetSection(StatusMonitorOptions.SectionName));
builder.Services.AddHttpClient("RavaApi");

var app = builder.Build();
var monitorStartedUtc = DateTime.UtcNow;

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/dashboard", async (
    IHttpClientFactory httpClientFactory,
    IOptions<StatusMonitorOptions> options,
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

    return Results.Ok(new DashboardResponse(
        DateTime.UtcNow,
        (DateTime.UtcNow - monitorStartedUtc).TotalSeconds,
        apiBaseUrl,
        monitor.GameUrl,
        monitor.ApiPublicUrl,
        monitor.StatusPublicUrl,
        apiReachable,
        apiResponseMs,
        apiError,
        apiStatus));
});

app.Logger.LogInformation("RAVA status dashboard listening on {Urls}", builder.Configuration["Urls"] ?? "http://0.0.0.0:6000");
app.Run();
