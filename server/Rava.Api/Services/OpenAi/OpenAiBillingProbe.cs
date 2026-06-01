using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;

namespace Rava.Api.Services.OpenAi;

public sealed class OpenAiBillingProbe(IOptions<OffworldNewsOptions> offworldNewsOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _gate = new();
    private DateTime _cachedUtc = DateTime.MinValue;
    private OpenAiBillingSnapshot _cached = OpenAiBillingSnapshot.Unavailable("Not loaded yet.");

    public async Task<OpenAiBillingSnapshot> GetCreditsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _cachedUtc < TimeSpan.FromMinutes(10))
            {
                return _cached;
            }
        }

        var snapshot = await FetchCreditsAsync(cancellationToken);

        lock (_gate)
        {
            _cached = snapshot;
            _cachedUtc = DateTime.UtcNow;
        }

        return snapshot;
    }

    private async Task<OpenAiBillingSnapshot> FetchCreditsAsync(CancellationToken cancellationToken)
    {
        var options = offworldNewsOptions.Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return OpenAiBillingSnapshot.Unavailable("OpenAI API key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? "https://api.openai.com/v1"
            : options.BaseUrl.TrimEnd('/');

        var billingUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/dashboard/billing/credit_grants"
            : $"{baseUrl}/dashboard/billing/credit_grants";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var request = new HttpRequestMessage(HttpMethod.Get, billingUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OpenAiBillingSnapshot.Unavailable(
                    $"Billing API HTTP {(int)response.StatusCode}. View balance at platform.openai.com.");
            }

            var parsed = JsonSerializer.Deserialize<CreditGrantsResponse>(payload, JsonOptions);
            var grants = parsed?.Grants?.Data ?? parsed?.Data ?? [];
            if (grants.Count == 0)
            {
                return OpenAiBillingSnapshot.Unavailable(
                    "No prepaid credit grants on this API key. Usage may be on a monthly invoice.");
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

            return new OpenAiBillingSnapshot(remaining, granted, null);
        }
        catch (Exception ex)
        {
            return OpenAiBillingSnapshot.Unavailable($"Could not load credits: {ex.Message}");
        }
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
}

public sealed record OpenAiBillingSnapshot(
    decimal? CreditsRemainingUsd,
    decimal? CreditsGrantedUsd,
    string? Note)
{
    public static OpenAiBillingSnapshot Unavailable(string note) => new(null, null, note);
}
