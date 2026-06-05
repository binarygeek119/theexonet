using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Api.Services.OffworldNews;
using Rava.Api.Services.OpenAi;
using Rava.Core.Services;

namespace Rava.Api.Services.Foreverfall;

public sealed class ForeverfallPortraitGenerator(
    OpenAiConnectionResolver openAi,
    HttpClient httpClient,
    string cacheRoot)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<(bool Ok, string? Error)> GenerateAndSaveAsync(
        string imageId,
        string displayName,
        string species,
        string gender,
        CancellationToken ct)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        var prompt = ForeverfallInmatePrompts.BuildPortraitPrompt(displayName, species, gender);
        if (prompt.Length > 3900)
        {
            prompt = prompt[..3900];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(openAi.BaseUrl, "/images/generations"));
        OpenAiUsageLoggingHandler.SetCategory(request, OpenAiUsageCategories.ForeverfallPortrait);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        request.Content = JsonContent.Create(
            OffworldNewsOpenAiImageRequest.BuildRequestBody(openAi.ImageModel, prompt, "1024x1024"));

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return (false, DescribeApiFailure((int)response.StatusCode, payload));
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
                return (false, $"Invalid base64 image data: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(imageData?.Url))
        {
            try
            {
                imageBytes = await httpClient.GetByteArrayAsync(imageData.Url, ct);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to download image: {ex.Message}");
            }
        }
        else
        {
            return (false, DescribeApiFailure(200, payload));
        }

        var filePath = ForeverfallStoragePaths.ImageFilePath(cacheRoot, imageId);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await OffworldNewsImageEncoder.SaveAsJpegAsync(imageBytes, filePath, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to save {filePath}: {ex.Message}");
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return $"{trimmed}/{path.TrimStart('/')}";
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
        public List<ImageData>? Data { get; init; }
    }

    private sealed class ImageData
    {
        public string? Url { get; init; }

        [JsonPropertyName("b64_json")]
        public string? B64Json { get; init; }
    }

    private sealed class OpenAiErrorResponse
    {
        public OpenAiErrorBody? Error { get; init; }
    }

    private sealed class OpenAiErrorBody
    {
        public string? Message { get; init; }
    }
}
