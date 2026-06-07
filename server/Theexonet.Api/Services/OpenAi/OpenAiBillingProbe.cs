using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.OpenAi;

public sealed class OpenAiBillingProbe(OpenAiConnectionResolver openAi)
{
    private const string BillingDashboardUrl = "https://platform.openai.com/settings/organization/billing";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _gate = new();
    private DateTime _cachedUtc = DateTime.MinValue;
    private TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
    private OpenAiBillingSnapshot _cached = OpenAiBillingSnapshot.Unavailable("Not loaded yet.");

    public async Task<OpenAiBillingSnapshot> GetCreditsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _cachedUtc < _cacheDuration)
            {
                return _cached;
            }
        }

        var snapshot = await FetchCreditsAsync(cancellationToken);

        lock (_gate)
        {
            _cached = snapshot;
            _cachedUtc = DateTime.UtcNow;
            _cacheDuration = snapshot.CreditsRemainingUsd is not null
                ? TimeSpan.FromMinutes(10)
                : snapshot.MonthToDateSpendUsd is not null
                    ? TimeSpan.FromMinutes(10)
                    : IsExpectedUnavailable(snapshot.Note)
                        ? TimeSpan.FromHours(24)
                        : TimeSpan.FromMinutes(10);
        }

        return snapshot;
    }

    private async Task<OpenAiBillingSnapshot> FetchCreditsAsync(CancellationToken cancellationToken)
    {
        var hasApiKey = openAi.IsApiKeyConfigured;
        var hasAdminKey = !string.IsNullOrWhiteSpace(openAi.AdminApiKey);

        if (!hasApiKey && !hasAdminKey)
        {
            return OpenAiBillingSnapshot.Unavailable("OpenAI API key is not configured.");
        }

        OpenAiBillingSnapshot? prepaidSnapshot = null;
        if (hasApiKey)
        {
            prepaidSnapshot = await TryFetchPrepaidCreditsAsync(cancellationToken);
            if (prepaidSnapshot.CreditsRemainingUsd is not null)
            {
                return prepaidSnapshot;
            }
        }

        if (hasAdminKey)
        {
            var spendSnapshot = await TryFetchMonthToDateSpendAsync(cancellationToken);
            if (spendSnapshot is not null)
            {
                return spendSnapshot;
            }
        }

        return prepaidSnapshot
            ?? OpenAiBillingSnapshot.Unavailable(
                "Could not load billing details. View balance on the OpenAI billing dashboard.");
    }

    private async Task<OpenAiBillingSnapshot> TryFetchPrepaidCreditsAsync(CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl(openAi.BaseUrl);
        var billingUrl = $"{baseUrl}/dashboard/billing/credit_grants";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var request = new HttpRequestMessage(HttpMethod.Get, billingUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OpenAiBillingSnapshot.Unavailable(
                    DescribeBillingFailure(response.StatusCode, payload));
            }

            var parsed = JsonSerializer.Deserialize<CreditGrantsResponse>(payload, JsonOptions);
            var grants = parsed?.Grants?.Data ?? parsed?.Data ?? [];
            if (grants.Count == 0)
            {
                return OpenAiBillingSnapshot.Unavailable(
                    "No prepaid credit grants on this API key. Usage may be on a monthly invoice. " +
                    $"View billing at {BillingDashboardUrl}.");
            }

            decimal remaining = 0;
            decimal granted = 0;
            foreach (var grant in grants)
            {
                var grantAmount = grant.GrantAmount ?? grant.Amount ?? 0;
                var usedAmount = grant.UsedAmount ?? 0;
                granted += grantAmount;
                remaining += Math.Max(0, grantAmount - usedAmount);
            }

            return new OpenAiBillingSnapshot(remaining, granted, null, null);
        }
        catch (Exception ex)
        {
            return OpenAiBillingSnapshot.Unavailable($"Could not load prepaid credits: {ex.Message}");
        }
    }

    private async Task<OpenAiBillingSnapshot?> TryFetchMonthToDateSpendAsync(CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl(openAi.BaseUrl);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startUnix = new DateTimeOffset(monthStart).ToUnixTimeSeconds();
        var endUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var costsUrl =
            $"{baseUrl}/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var request = new HttpRequestMessage(HttpMethod.Get, costsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.AdminApiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var parsed = JsonSerializer.Deserialize<OrganizationCostsResponse>(payload, JsonOptions);
            var spend = SumOrganizationCosts(parsed);
            if (spend is null)
            {
                return null;
            }

            var monthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            return new OpenAiBillingSnapshot(
                null,
                null,
                $"Month-to-date API spend for {monthLabel} (admin key). Account balance: {BillingDashboardUrl}.",
                spend);
        }
        catch
        {
            return null;
        }
    }

    private static decimal? SumOrganizationCosts(OrganizationCostsResponse? response)
    {
        if (response?.Data is null || response.Data.Count == 0)
        {
            return 0m;
        }

        decimal total = 0;
        var sawAmount = false;
        foreach (var bucket in response.Data)
        {
            foreach (var result in bucket.Results ?? [])
            {
                if (result.Amount?.Value is { } amount)
                {
                    total += amount;
                    sawAmount = true;
                }
            }
        }

        return sawAmount ? total : 0m;
    }

    private static string ResolveBaseUrl(string? configuredBaseUrl) =>
        string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://api.openai.com/v1"
            : configuredBaseUrl.TrimEnd('/');

    private static string DescribeBillingFailure(HttpStatusCode statusCode, string payload)
    {
        var apiMessage = TryReadOpenAiErrorMessage(payload);
        if (IsSecretKeyBillingBlock(statusCode, apiMessage))
        {
            return
                "Account balance is only visible on the OpenAI billing dashboard, not via a secret API key. " +
                "Game AI features are unaffected.";
        }

        if (!string.IsNullOrWhiteSpace(apiMessage))
        {
            return $"{apiMessage} View billing at {BillingDashboardUrl}.";
        }

        if (statusCode == HttpStatusCode.Forbidden)
        {
            return
                "Billing details are not available with this API key (OpenAI HTTP 403). " +
                "Check the billing dashboard. Game AI features are unaffected.";
        }

        return $"Billing API HTTP {(int)statusCode}. View billing at {BillingDashboardUrl}.";
    }

    private static bool IsSecretKeyBillingBlock(HttpStatusCode statusCode, string? apiMessage) =>
        statusCode == HttpStatusCode.Forbidden
        || apiMessage?.Contains("session key", StringComparison.OrdinalIgnoreCase) == true
        || apiMessage?.Contains("secret", StringComparison.OrdinalIgnoreCase) == true
            && apiMessage.Contains("browser", StringComparison.OrdinalIgnoreCase);

    private static string? TryReadOpenAiErrorMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<OpenAiErrorResponse>(payload, JsonOptions);
            return parsed?.Error?.Message?.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsExpectedUnavailable(string? note) =>
        note?.Contains("billing dashboard", StringComparison.OrdinalIgnoreCase) == true
        || note?.Contains("secret API key", StringComparison.OrdinalIgnoreCase) == true
        || note?.Contains("No prepaid credit grants", StringComparison.OrdinalIgnoreCase) == true
        || note?.Contains("monthly invoice", StringComparison.OrdinalIgnoreCase) == true
        || note?.Contains("Game AI features are unaffected", StringComparison.OrdinalIgnoreCase) == true;

    private sealed class OpenAiErrorResponse
    {
        public OpenAiErrorBody? Error { get; set; }
    }

    private sealed class OpenAiErrorBody
    {
        public string? Message { get; set; }
    }

    private sealed class CreditGrantsResponse
    {
        public CreditGrantsContainer? Grants { get; set; }

        [JsonPropertyName("data")]
        public List<CreditGrant>? Data { get; set; }
    }

    private sealed class CreditGrantsContainer
    {
        [JsonPropertyName("data")]
        public List<CreditGrant>? Data { get; set; }
    }

    private sealed class CreditGrant
    {
        [JsonPropertyName("grant_amount")]
        public decimal? GrantAmount { get; set; }

        [JsonPropertyName("used_amount")]
        public decimal? UsedAmount { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }
    }

    private sealed class OrganizationCostsResponse
    {
        [JsonPropertyName("data")]
        public List<OrganizationCostsBucket>? Data { get; set; }
    }

    private sealed class OrganizationCostsBucket
    {
        [JsonPropertyName("results")]
        public List<OrganizationCostsResult>? Results { get; set; }
    }

    private sealed class OrganizationCostsResult
    {
        [JsonPropertyName("amount")]
        public OrganizationCostsAmount? Amount { get; set; }
    }

    private sealed class OrganizationCostsAmount
    {
        [JsonPropertyName("value")]
        public decimal? Value { get; set; }
    }
}

public sealed record OpenAiBillingSnapshot(
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    string? Note,
    decimal? MonthToDateSpendUsd = null)
{
    public static OpenAiBillingSnapshot Unavailable(string note) => new(null, null, note);
}
