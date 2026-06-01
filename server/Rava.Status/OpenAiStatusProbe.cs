using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Rava.Status;

public static class OpenAiStatusProbe
{
    public const string DefaultSummaryUrl = "https://status.openai.com/api/v2/summary.json";
    public const string DefaultStatusPageUrl = "https://status.openai.com/";

    public static async Task<OpenAiStatusPayload> FetchAsync(
        HttpClient client,
        string summaryUrl,
        string statusPageUrl,
        CancellationToken cancellationToken)
    {
        summaryUrl = string.IsNullOrWhiteSpace(summaryUrl) ? DefaultSummaryUrl : summaryUrl.Trim();
        statusPageUrl = string.IsNullOrWhiteSpace(statusPageUrl) ? DefaultStatusPageUrl : statusPageUrl.TrimEnd('/');

        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync(summaryUrl, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new OpenAiStatusPayload(
                    statusPageUrl,
                    summaryUrl,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    null,
                    null,
                    [],
                    [],
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<StatuspageSummaryResponse>(cancellationToken);
            if (parsed?.Status is null)
            {
                return new OpenAiStatusPayload(
                    statusPageUrl,
                    summaryUrl,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    null,
                    null,
                    [],
                    [],
                    "Status page returned an unexpected payload.");
            }

            var allComponents = (parsed.Components ?? [])
                .Select(component => new OpenAiComponentStatusPayload(
                    component.Name ?? "Unknown",
                    FormatComponentStatus(component.Status)))
                .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var degradedComponents = allComponents
                .Where(component =>
                    !string.Equals(component.Status, "operational", StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .ToList();

            return new OpenAiStatusPayload(
                statusPageUrl,
                summaryUrl,
                true,
                stopwatch.ElapsedMilliseconds,
                parsed.Status.Indicator,
                parsed.Status.Description,
                degradedComponents,
                allComponents,
                null);
        }
        catch (Exception ex)
        {
            return new OpenAiStatusPayload(
                statusPageUrl,
                summaryUrl,
                false,
                null,
                null,
                null,
                [],
                [],
                ex.Message);
        }
    }

    private static string FormatComponentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "unknown";
        }

        return status.Replace('_', ' ');
    }

    private sealed class StatuspageSummaryResponse
    {
        public StatuspageStatusBody? Status { get; set; }
        public List<StatuspageComponent>? Components { get; set; }
    }

    private sealed class StatuspageStatusBody
    {
        public string? Indicator { get; set; }
        public string? Description { get; set; }
    }

    private sealed class StatuspageComponent
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
    }
}
