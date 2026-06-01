using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsService(
    IWebHostEnvironment environment,
    IOptions<OffworldNewsOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<OffworldNewsService> logger)
{
    private static readonly ConcurrentDictionary<DateOnly, SemaphoreSlim> GenerationLocks = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly OffworldNewsOptions _options = options.Value;

    public async Task<OffworldNewsEditionDto> GetEditionAsync(DateOnly? editionDate = null, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return OffworldNewsTemplateGenerator.Generate(editionDate ?? UtcGameClock.Today, _options.StoriesPerDay);
        }

        var date = editionDate ?? UtcGameClock.Today;
        var cached = TryLoadEdition(date);
        if (cached is not null)
        {
            return cached;
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            cached = TryLoadEdition(date);
            if (cached is not null)
            {
                return cached;
            }

            var edition = await GenerateAndStoreEditionAsync(date, ct);
            return edition;
        }
        finally
        {
            gate.Release();
        }
    }

    private OffworldNewsEditionDto? TryLoadEdition(DateOnly date)
    {
        var path = GetEditionFilePath(date);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OffworldNewsEditionDto>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Offworld News edition cache for {Date}", date);
            return null;
        }
    }

    private async Task<OffworldNewsEditionDto> GenerateAndStoreEditionAsync(DateOnly date, CancellationToken ct)
    {
        OffworldNewsEditionDto edition;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogInformation(
                "OffworldNews.ApiKey not configured; using template edition for {Date}",
                date);
            edition = OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay);
        }
        else
        {
            var generator = new OpenAiOffworldNewsGenerator(
                _options,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            edition = await generator.GenerateAsync(date, GetCacheRoot(), ct);
        }

        SaveEdition(edition);
        return edition;
    }

    private void SaveEdition(OffworldNewsEditionDto edition)
    {
        EnsureCacheDirectories(edition.EditionDate);
        var path = GetEditionFilePath(edition.EditionDate);
        var json = JsonSerializer.Serialize(edition, JsonOptions);
        File.WriteAllText(path, json);
    }

    private string GetCacheRoot()
    {
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(environment.ContentRootPath, "html");
        }

        return Path.Combine(webRoot, _options.CacheDirectory);
    }

    private string GetEditionFilePath(DateOnly date) =>
        Path.Combine(GetCacheRoot(), "editions", $"{date:yyyy-MM-dd}.json");

    private void EnsureCacheDirectories(DateOnly date)
    {
        var root = GetCacheRoot();
        Directory.CreateDirectory(Path.Combine(root, "editions"));
        Directory.CreateDirectory(Path.Combine(root, "images", date.ToString("yyyy-MM-dd")));
    }
}
