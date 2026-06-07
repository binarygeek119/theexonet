using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Theexonet.Api.Services.CompanyLogo;
using Theexonet.Api.Services.OpenAi;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.TestingDummyFriends;

public sealed class TestingDummyFriendsAssetGenerator(
    OpenAiConnectionResolver openAi,
    IOptions<CompanyLogoOptions> companyLogoOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<TestingDummyFriendsAssetGenerator> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public bool IsConfigured => openAi.IsApiKeyConfigured;

    public async Task<(bool Ok, string? Error)> GenerateAvatarAsync(
        TestingDummyFriendsProfile profile,
        string assetsRoot,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        Directory.CreateDirectory(TestingDummyFriendsPaths.ProfileFolder(assetsRoot, profile.Index));
        return await GenerateJpegAsync(
            TestingDummyFriendsPortraitPrompts.BuildAvatarPrompt(profile),
            TestingDummyFriendsPaths.AvatarFilePath(assetsRoot, profile.Index),
            "1024x1024",
            OpenAiUsageCategories.TestingDummyAvatar,
            ct);
    }

    public async Task<(bool Ok, string? Error)> GenerateBackgroundAsync(
        TestingDummyFriendsProfile profile,
        string assetsRoot,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        Directory.CreateDirectory(TestingDummyFriendsPaths.ProfileFolder(assetsRoot, profile.Index));
        return await GenerateJpegAsync(
            TestingDummyFriendsPortraitPrompts.BuildBackgroundPrompt(profile),
            TestingDummyFriendsPaths.BackgroundFilePath(assetsRoot, profile.Index),
            "1792x1024",
            OpenAiUsageCategories.TestingDummyBackground,
            ct);
    }

    public async Task<(bool Ok, string? Error)> GenerateLogoAsync(
        TestingDummyFriendsProfile profile,
        string assetsRoot,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        Directory.CreateDirectory(TestingDummyFriendsPaths.ProfileFolder(assetsRoot, profile.Index));
        return await GenerateLogoFileAsync(
            profile,
            TestingDummyFriendsPaths.LogoFilePath(assetsRoot, profile.Index),
            ct);
    }

    public async Task<(int Attempted, int Succeeded, string? LastError)> EnsureProfileAssetsAsync(
        TestingDummyFriendsProfile profile,
        string assetsRoot,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return (0, 0, "OpenAi.ApiKey is not configured.");
        }

        Directory.CreateDirectory(TestingDummyFriendsPaths.ProfileFolder(assetsRoot, profile.Index));
        var attempted = 0;
        var succeeded = 0;
        string? lastError = null;

        var avatarPath = TestingDummyFriendsPaths.AvatarFilePath(assetsRoot, profile.Index);
        if (!File.Exists(avatarPath))
        {
            attempted++;
            var (ok, error) = await GenerateJpegAsync(
                TestingDummyFriendsPortraitPrompts.BuildAvatarPrompt(profile),
                avatarPath,
                "1024x1024",
                OpenAiUsageCategories.TestingDummyAvatar,
                ct);
            if (ok)
            {
                succeeded++;
            }
            else
            {
                lastError = error;
                logger.LogWarning("Testing dummy avatar failed for {Username}: {Error}", profile.Username, error);
            }
        }

        var backgroundPath = TestingDummyFriendsPaths.BackgroundFilePath(assetsRoot, profile.Index);
        if (!File.Exists(backgroundPath))
        {
            attempted++;
            var (ok, error) = await GenerateJpegAsync(
                TestingDummyFriendsPortraitPrompts.BuildBackgroundPrompt(profile),
                backgroundPath,
                "1792x1024",
                OpenAiUsageCategories.TestingDummyBackground,
                ct);
            if (ok)
            {
                succeeded++;
            }
            else
            {
                lastError = error;
                logger.LogWarning("Testing dummy background failed for {Username}: {Error}", profile.Username, error);
            }
        }

        var logoPath = TestingDummyFriendsPaths.LogoFilePath(assetsRoot, profile.Index);
        if (!File.Exists(logoPath))
        {
            attempted++;
            var (ok, error) = await GenerateLogoFileAsync(profile, logoPath, ct);
            if (ok)
            {
                succeeded++;
            }
            else
            {
                lastError = error;
                logger.LogWarning("Testing dummy logo failed for {Username}: {Error}", profile.Username, error);
            }
        }

        return (attempted, succeeded, lastError);
    }

    private async Task<(bool Ok, string? Error)> GenerateJpegAsync(
        string prompt,
        string filePath,
        string size,
        string category,
        CancellationToken ct)
    {
        if (prompt.Length > 3900)
        {
            prompt = prompt[..3900];
        }

        var httpClient = httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(openAi.BaseUrl, "/images/generations"));
        OpenAiUsageLoggingHandler.SetCategory(request, category);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        request.Content = JsonContent.Create(
            OffworldNewsOpenAiImageRequest.BuildRequestBody(openAi.ImageModel, prompt, size));

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return (false, DescribeApiFailure((int)response.StatusCode, payload));
        }

        var imageBytes = await ReadImageBytesAsync(httpClient, payload, ct);
        if (imageBytes is null)
        {
            return (false, DescribeApiFailure(200, payload));
        }

        try
        {
            await OffworldNewsImageEncoder.SaveAsJpegAsync(imageBytes, filePath, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to save {filePath}: {ex.Message}");
        }
    }

    private async Task<(bool Ok, string? Error)> GenerateLogoFileAsync(
        TestingDummyFriendsProfile profile,
        string filePath,
        CancellationToken ct)
    {
        var logoOptions = companyLogoOptions.Value;
        if (!logoOptions.Enabled)
        {
            return (false, "AI logo generation is disabled.");
        }

        var prompt = CompanyLogoPrompts.Build(
            profile.MineName,
            profile.Username,
            profile.Mood,
            profile.AboutMe,
            profile.Interests,
            profile.Music);
        if (prompt.Length > 3900)
        {
            prompt = prompt[..3900];
        }

        var baseUrl = openAi.BaseUrlForCompanyLogo(logoOptions);
        var imageModel = openAi.ImageModelForCompanyLogo(logoOptions);

        var httpClient = httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(baseUrl, "/images/generations"));
        OpenAiUsageLoggingHandler.SetCategory(request, OpenAiUsageCategories.TestingDummyLogo);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        request.Content = JsonContent.Create(CompanyLogoOpenAiImageRequest.BuildRequestBody(imageModel, prompt));

        using var response = await httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return (false, DescribeApiFailure((int)response.StatusCode, payload));
        }

        var imageBytes = await ReadImageBytesAsync(httpClient, payload, ct);
        if (imageBytes is null)
        {
            return (false, DescribeApiFailure(200, payload));
        }

        try
        {
            var pngBytes = await CompanyLogoImageEncoder.PrepareTransparentPngAsync(imageBytes, ct);
            await File.WriteAllBytesAsync(filePath, pngBytes, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to save {filePath}: {ex.Message}");
        }
    }

    private async Task<byte[]?> ReadImageBytesAsync(HttpClient httpClient, string payload, CancellationToken ct)
    {
        var parsed = JsonSerializer.Deserialize<ImageGenerationResponse>(payload, SerializerOptions);
        var imageData = parsed?.Data?.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(imageData?.B64Json))
        {
            try
            {
                return Convert.FromBase64String(imageData.B64Json);
            }
            catch (FormatException ex)
            {
                logger.LogWarning("Invalid base64 image data: {Error}", ex.Message);
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(imageData?.Url))
        {
            try
            {
                return await httpClient.GetByteArrayAsync(imageData.Url, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to download image: {Error}", ex.Message);
                return null;
            }
        }

        return null;
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
                return $"OpenAI HTTP {status}: {parsed.Error.Message}";
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }

        return $"OpenAI HTTP {status}: {payload}";
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
        public OpenAiError? Error { get; init; }
    }

    private sealed class OpenAiError
    {
        public string? Message { get; init; }
    }
}
