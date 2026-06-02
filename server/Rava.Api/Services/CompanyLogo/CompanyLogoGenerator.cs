using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Core.Validation;
using Rava.Api.Services.OpenAi;
using Rava.Api.Services.OffworldNews;

namespace Rava.Api.Services.CompanyLogo;

public sealed class CompanyLogoGenerator(
    IOptions<CompanyLogoOptions> companyLogoOptions,
    IOptions<OffworldNewsOptions> offworldNewsOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<CompanyLogoGenerator> logger) : ICompanyLogoGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public async Task<(byte[]? PngBytes, string? Error)> GenerateAsync(
        string companyName,
        string username,
        string mood,
        string aboutMe,
        string interests,
        string music,
        CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (null, "AI logo generation is not configured on this server.");
        }

        var options = companyLogoOptions.Value;
        if (!options.Enabled)
        {
            return (null, "AI logo generation is disabled.");
        }

        var prompt = CompanyLogoPrompts.Build(companyName, username, mood, aboutMe, interests, music);
        if (prompt.Length > 3900)
        {
            prompt = prompt[..3900];
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? offworldNewsOptions.Value.BaseUrl
            : options.BaseUrl;
        var imageModel = string.IsNullOrWhiteSpace(options.ImageModel)
            ? offworldNewsOptions.Value.ImageModel
            : options.ImageModel;

        var httpClient = httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(baseUrl, "/images/generations"));
        OpenAiUsageLoggingHandler.SetCategory(request, OpenAiUsageCategories.CompanyLogo);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(CompanyLogoOpenAiImageRequest.BuildRequestBody(imageModel, prompt));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, DescribeApiFailure((int)response.StatusCode, payload));
        }

        var parsed = JsonSerializer.Deserialize<ImageGenerationResponse>(payload, SerializerOptions);
        var imageData = parsed?.Data?.FirstOrDefault();
        byte[]? imageBytes = null;

        if (!string.IsNullOrWhiteSpace(imageData?.B64Json))
        {
            try
            {
                imageBytes = Convert.FromBase64String(imageData.B64Json);
            }
            catch (FormatException ex)
            {
                return (null, $"Invalid base64 image data: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(imageData?.Url))
        {
            try
            {
                imageBytes = await httpClient.GetByteArrayAsync(imageData.Url, cancellationToken);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to download image: {ex.Message}");
            }
        }
        else
        {
            return (null, DescribeMissingImageData(payload));
        }

        try
        {
            var png = await CompanyLogoImageEncoder.PrepareTransparentPngAsync(imageBytes, cancellationToken);
            var validationError = CompanyLogoValidator.Validate("image/png", png);
            if (validationError is not null)
            {
                logger.LogWarning("Generated company logo failed validation: {Error}", validationError);
                return (null, "Generated image was not a valid transparent PNG. Try again later.");
            }

            return (png, null);
        }
        catch (Exception ex)
        {
            return (null, $"Failed to process logo image: {ex.Message}");
        }
    }

    private string? ResolveApiKey()
    {
        var key = companyLogoOptions.Value.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return offworldNewsOptions.Value.ApiKey?.Trim();
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/{path.TrimStart('/')}";
    }

    private static string DescribeMissingImageData(string payload)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<ImageGenerationResponse>(payload, SerializerOptions);
            if (parsed?.Data is { Count: > 0 })
            {
                return "OpenAI returned a success response but no usable image data (expected b64_json or url).";
            }
        }
        catch
        {
            // ignore parse errors
        }

        return DescribeApiFailure(200, payload);
    }

    private static string DescribeApiFailure(int status, string payload)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<OpenAiErrorResponse>(payload, SerializerOptions);
            if (!string.IsNullOrWhiteSpace(parsed?.Error?.Message))
            {
                return $"OpenAI HTTP {status}: {parsed.Error.Message.Trim()}";
            }
        }
        catch
        {
            // ignore parse errors
        }

        var trimmed = payload.Trim();
        return trimmed.Length == 0
            ? $"OpenAI HTTP {status} with empty response body."
            : $"OpenAI HTTP {status}: {trimmed[..Math.Min(trimmed.Length, 240)]}";
    }

    private sealed class ImageGenerationResponse
    {
        public List<ImageData>? Data { get; set; }
    }

    private sealed class ImageData
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        public string? Url { get; set; }
    }

    private sealed class OpenAiErrorResponse
    {
        public OpenAiErrorBody? Error { get; set; }
    }

    private sealed class OpenAiErrorBody
    {
        public string? Message { get; set; }
    }
}
