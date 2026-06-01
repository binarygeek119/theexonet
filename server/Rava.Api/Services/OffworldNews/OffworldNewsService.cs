using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;
using Rava.Infrastructure.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsService(
    IWebHostEnvironment environment,
    IOptions<OffworldNewsOptions> options,
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<OffworldNewsService> logger)
{
    private static readonly ConcurrentDictionary<DateOnly, SemaphoreSlim> GenerationLocks = new();
    private static readonly ConcurrentDictionary<DateOnly, byte> BackgroundUpgradeQueued = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly OffworldNewsOptions _options = options.Value;

    public async Task<OffworldNewsEditionDto> GetEditionAsync(DateOnly? editionDate = null, CancellationToken ct = default)
    {
        var date = editionDate ?? UtcGameClock.Today;

        if (!_options.Enabled)
        {
            return OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay);
        }

        var cached = TryLoadEdition(date);
        if (cached is not null)
        {
            return EnsureStoryImages(cached);
        }

        await EnsureEditionAsync(date, forceRegenerate: false, ct);

        cached = TryLoadEdition(date);
        if (cached is not null)
        {
            return EnsureStoryImages(cached);
        }

        var companyContext = await LoadCompanyContextAsync(ct);
        return OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay, companyContext);
    }

    public OffworldNewsArchivesDto ListArchives()
    {
        if (!_options.Enabled)
        {
            return new OffworldNewsArchivesDto([]);
        }

        var editionsDir = Path.Combine(GetCacheRoot(), "editions");
        if (!Directory.Exists(editionsDir))
        {
            return new OffworldNewsArchivesDto([]);
        }

        var entries = new List<OffworldNewsArchiveEntryDto>();
        foreach (var path in Directory.EnumerateFiles(editionsDir, "*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!DateOnly.TryParse(fileName, out var editionDate))
            {
                continue;
            }

            var headline = (string?)null;
            var storyCount = 0;
            try
            {
                var edition = JsonSerializer.Deserialize<OffworldNewsEditionDto>(File.ReadAllText(path), JsonOptions);
                if (edition is not null)
                {
                    storyCount = edition.Stories.Count;
                    headline = edition.Stories.FirstOrDefault()?.Headline;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Offworld News archive index for {Date}", editionDate);
            }

            entries.Add(new OffworldNewsArchiveEntryDto(editionDate, storyCount, headline));
        }

        entries.Sort((left, right) => right.EditionDate.CompareTo(left.EditionDate));
        return new OffworldNewsArchivesDto(entries);
    }

    /// <summary>
    /// Creates today's edition on startup, or a fresh edition after UTC midnight when <paramref name="forceRegenerate"/> is true.
    /// </summary>
    public async Task EnsureEditionAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        EnsureCacheDirectories(date);

        if (!forceRegenerate)
        {
            var existing = TryLoadEdition(date);
            if (existing is not null)
            {
                if (!string.Equals(existing.Source, "openai", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    QueueBackgroundAiUpgrade(date);
                }

                return;
            }
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRegenerate && TryLoadEdition(date) is not null)
            {
                return;
            }

            if (forceRegenerate)
            {
                BackgroundUpgradeQueued.TryRemove(date, out _);
            }

            var companyContext = await LoadCompanyContextAsync(ct);
            var template = OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay, companyContext);
            TrySaveEdition(template);

            logger.LogInformation(
                "Offworld News template edition ready for {Date} (force={Force})",
                date,
                forceRegenerate);

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                QueueBackgroundAiUpgrade(date);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<OffworldNewsCompanyContext?> LoadCompanyContextAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var profiles = scope.ServiceProvider.GetRequiredService<PublicProfileService>();
            return await profiles.GetNewsCompanyContextAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load player company names for Offworld News");
            return null;
        }
    }

    private OffworldNewsEditionDto EnsureStoryImages(OffworldNewsEditionDto edition)
    {
        var stories = edition.Stories
            .Select(story => string.IsNullOrWhiteSpace(story.ImageUrl)
                ? story with
                {
                    ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                }
                : story)
            .ToList();

        return edition with { Stories = stories };
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
        var companyContext = await LoadCompanyContextAsync(ct);
        OffworldNewsEditionDto edition;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogInformation(
                "OffworldNews.ApiKey not configured; using template edition for {Date}",
                date);
            edition = OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay, companyContext);
        }
        else
        {
            var generator = new OpenAiOffworldNewsGenerator(
                _options,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            edition = await generator.GenerateAsync(date, GetCacheRoot(), companyContext, ct);
        }

        edition = EnsureStoryImages(edition);
        TrySaveEdition(edition);
        return edition;
    }

    private void QueueBackgroundAiUpgrade(DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return;
        }

        if (!BackgroundUpgradeQueued.TryAdd(date, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync();
                try
                {
                    var cached = TryLoadEdition(date);
                    if (string.Equals(cached?.Source, "openai", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    logger.LogInformation("Generating Offworld News AI edition for {Date} in background", date);
                    await GenerateAndStoreEditionAsync(date, CancellationToken.None);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background Offworld News AI upgrade failed for {Date}", date);
            }
            finally
            {
                BackgroundUpgradeQueued.TryRemove(date, out _);
            }
        });
    }

    private void TrySaveEdition(OffworldNewsEditionDto edition)
    {
        try
        {
            EnsureCacheDirectories(edition.EditionDate);
            var path = GetEditionFilePath(edition.EditionDate);
            var json = JsonSerializer.Serialize(edition, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write Offworld News edition cache for {Date} under {Root}",
                edition.EditionDate,
                GetCacheRoot());
        }
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
