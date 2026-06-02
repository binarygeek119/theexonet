using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Api.Services.OpenAi;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsReporterPortraitGenerator(
    OffworldNewsOptions options,
    HttpClient httpClient,
    string reportersAssetsRoot,
    ILogger logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<OffworldNewsReporterPortraitGenerationSummary> GenerateAllAsync(
        IReadOnlyList<string>? slugs = null,
        ReporterPortraitAssetKind assets = ReporterPortraitAssetKind.Both,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return OffworldNewsReporterPortraitGenerationSummary.Failed("OffworldNews.ApiKey is not configured.");
        }

        Directory.CreateDirectory(reportersAssetsRoot);
        var targets = ResolveTargets(slugs).ToList();
        var attempted = 0;
        var succeeded = 0;
        string? lastError = null;

        foreach (var reporter in targets)
        {
            ct.ThrowIfCancellationRequested();
            var folder = OffworldNewsReporterPaths.ReporterFolder(reportersAssetsRoot, reporter.Slug);
            Directory.CreateDirectory(folder);

            foreach (var (prompt, filePath, size, category) in PortraitJobs(reporter, assets))
            {
                attempted++;
                var (ok, error) = await GenerateAndSaveAsync(prompt, filePath, size, category, ct);
                if (ok)
                {
                    succeeded++;
                }
                else
                {
                    lastError = error;
                    logger.LogWarning(
                        "Reporter portrait generation failed for {Slug} at {Path}: {Error}",
                        reporter.Slug,
                        filePath,
                        error);
                }
            }
        }

        return new OffworldNewsReporterPortraitGenerationSummary(
            targets.Count,
            attempted,
            succeeded,
            lastError);
    }

    private static IEnumerable<OffworldNewsReporterProfile> ResolveTargets(IReadOnlyList<string>? slugs)
    {
        if (slugs is null || slugs.Count == 0)
        {
            return OffworldNewsReporterCatalog.All;
        }

        return slugs
            .Select(OffworldNewsReporterCatalog.TryGetBySlug)
            .Where(reporter => reporter is not null)
            .Cast<OffworldNewsReporterProfile>()
            .ToList();
    }

    private IEnumerable<(string Prompt, string FilePath, string Size, string Category)> PortraitJobs(
        OffworldNewsReporterProfile reporter,
        ReporterPortraitAssetKind assets)
    {
        if (assets is ReporterPortraitAssetKind.Both or ReporterPortraitAssetKind.Avatar)
        {
            yield return (
                OffworldNewsReporterPortraitPrompts.BuildAvatarPrompt(reporter),
                OffworldNewsReporterPaths.AvatarFilePath(reportersAssetsRoot, reporter.Slug),
                "1024x1024",
                OpenAiUsageCategories.ReporterAvatar);
        }

        if (assets is ReporterPortraitAssetKind.Both or ReporterPortraitAssetKind.Background)
        {
            yield return (
                OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter),
                OffworldNewsReporterPaths.BackgroundFilePath(reportersAssetsRoot, reporter.Slug),
                "1792x1024",
                OpenAiUsageCategories.ReporterBackground);
        }
    }

    private async Task<(bool Ok, string? Error)> GenerateAndSaveAsync(
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

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(options.BaseUrl, "/images/generations"));
        OpenAiUsageLoggingHandler.SetCategory(request, category);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(
            OffworldNewsOpenAiImageRequest.BuildRequestBody(options.ImageModel, prompt, size));

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

public sealed record OffworldNewsReporterPortraitGenerationSummary(
    int ReporterCount,
    int Attempted,
    int Succeeded,
    string? Error)
{
    public static OffworldNewsReporterPortraitGenerationSummary Failed(string error) =>
        new(0, 0, 0, error);

    public string Describe() =>
        Succeeded == Attempted
            ? $"Generated {Succeeded} reporter images for {ReporterCount} correspondents."
            : $"Generated {Succeeded}/{Attempted} reporter images. Last error: {Error}";
}
