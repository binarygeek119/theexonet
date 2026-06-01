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
    IOptions<OffworldNewsOptions> options,
    RavaHostingPaths hostingPaths,
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
    private readonly string _cacheRoot = hostingPaths.OffworldNewsCacheRoot;

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
            return EnsureStoryImages(EnrichEditionAuthors(cached));
        }

        await EnsureEditionAsync(date, forceRegenerate: false, ct);

        cached = TryLoadEdition(date);
        if (cached is not null)
        {
            return EnsureStoryImages(EnrichEditionAuthors(cached));
        }

        var companyContext = await LoadCompanyContextAsync(ct);
        return EnrichEditionAuthors(
            OffworldNewsTemplateGenerator.Generate(date, _options.StoriesPerDay, companyContext));
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

    public OffworldNewsReportersDto ListReporters() =>
        new(OffworldNewsReporterCatalog.All.Select(MapReporterDto).ToList());

    public IReadOnlyList<OffworldNewsReporterDto> SearchReporters(string query, int limit = 20) =>
        OffworldNewsReporterCatalog.Search(query, limit).Select(MapReporterDto).ToList();

    public OffworldNewsReporterDetailDto? GetReporterDetail(string slug, int storyLimit = 15)
    {
        var reporter = OffworldNewsReporterCatalog.Resolve(Uri.UnescapeDataString(slug));
        if (reporter is null)
        {
            return null;
        }

        var stories = ListStoriesByReporter(reporter.DisplayName, storyLimit);
        return new OffworldNewsReporterDetailDto(MapReporterDto(reporter), stories);
    }

    public async Task<(OffworldNewsReporterPortraitGenerationSummary? Summary, string? Error)> RegenerateReporterPortraitsAsync(
        IReadOnlyList<string>? slugs = null,
        ReporterPortraitAssetKind assets = ReporterPortraitAssetKind.Both,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return (null, "Offworld News is disabled in configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return (null, "OffworldNews.ApiKey is not configured.");
        }

        if (slugs is { Count: > 0 } && slugs.All(slug => OffworldNewsReporterCatalog.TryGetBySlug(slug) is null))
        {
            return (null, "Reporter not found.");
        }

        var generator = new OffworldNewsReporterPortraitGenerator(
            _options,
            httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
            hostingPaths.OffworldNewsReportersAssetsRoot,
            logger);

        var summary = await generator.GenerateAllAsync(slugs, assets, ct);
        return summary.Succeeded == 0
            ? (summary, summary.Error ?? "No reporter portraits were generated.")
            : (summary, null);
    }

    private IReadOnlyList<OffworldNewsReporterStoryRefDto> ListStoriesByReporter(string displayName, int limit)
    {
        limit = Math.Clamp(limit, 1, 50);
        var matches = new List<OffworldNewsReporterStoryRefDto>();
        var editionsDir = Path.Combine(GetCacheRoot(), "editions");
        if (!Directory.Exists(editionsDir))
        {
            return matches;
        }

        foreach (var path in Directory.EnumerateFiles(editionsDir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            if (matches.Count >= limit)
            {
                break;
            }

            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!DateOnly.TryParse(fileName, out var editionDate))
            {
                continue;
            }

            OffworldNewsEditionDto? edition;
            try
            {
                edition = JsonSerializer.Deserialize<OffworldNewsEditionDto>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Offworld News edition for reporter index {Date}", editionDate);
                continue;
            }

            if (edition is null)
            {
                continue;
            }

            var isArchive = editionDate < UtcGameClock.Today;
            foreach (var story in edition.Stories)
            {
                if (!string.Equals(story.Author, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add(new OffworldNewsReporterStoryRefDto(
                    editionDate,
                    story.Id,
                    story.Headline,
                    story.Category,
                    story.PublishedAt,
                    isArchive));

                if (matches.Count >= limit)
                {
                    break;
                }
            }
        }

        return matches
            .OrderByDescending(story => story.EditionDate)
            .ThenByDescending(story => story.PublishedAt)
            .Take(limit)
            .ToList();
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

    private static OffworldNewsEditionDto EnrichEditionAuthors(OffworldNewsEditionDto edition)
    {
        var stories = edition.Stories.Select(EnrichStoryAuthor).ToList();
        return edition with { Stories = stories };
    }

    private static OffworldNewsStoryDto EnrichStoryAuthor(OffworldNewsStoryDto story)
    {
        var reporter = OffworldNewsReporterCatalog.Resolve(story.AuthorSlug)
            ?? OffworldNewsReporterCatalog.Resolve(story.Author);
        if (reporter is null)
        {
            return story;
        }

        return story with
        {
            Author = reporter.DisplayName,
            AuthorSlug = reporter.Slug,
        };
    }

    private OffworldNewsEditionDto EnsureStoryImages(OffworldNewsEditionDto edition)
    {
        var stories = edition.Stories
            .Select(story => SanitizeStoryImage(story))
            .ToList();

        return edition with { Stories = stories };
    }

    private OffworldNewsStoryDto SanitizeStoryImage(OffworldNewsStoryDto story)
    {
        if (string.IsNullOrWhiteSpace(story.ImageUrl))
        {
            return story with
            {
                ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                ImageAspect = null,
            };
        }

        if (OffworldNewsImagePaths.IsGeneratedImageUrl(story.ImageUrl)
            && !OffworldNewsImagePaths.GeneratedImageExists(GetCacheRoot(), story.ImageUrl))
        {
            return story with
            {
                ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                ImageAspect = null,
            };
        }

        return story;
    }

    private OffworldNewsEditionDto ResetGeneratedImagesToPlaceholders(OffworldNewsEditionDto edition)
    {
        var stories = edition.Stories
            .Select(story => OffworldNewsImagePaths.IsGeneratedImageUrl(story.ImageUrl)
                ? story with
                {
                    ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                    ImageAspect = null,
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

        edition = EnrichEditionAuthors(EnsureStoryImages(edition));
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

    private OffworldNewsReporterDto MapReporterDto(OffworldNewsReporterProfile reporter) =>
        OffworldNewsReporterCatalog.ToDto(reporter, hostingPaths.ReporterAssetRoots());

    private string GetCacheRoot() => _cacheRoot;

    private string GetEditionFilePath(DateOnly date) =>
        Path.Combine(GetCacheRoot(), "editions", $"{date:yyyy-MM-dd}.json");

    private void EnsureCacheDirectories(DateOnly date)
    {
        var root = GetCacheRoot();
        Directory.CreateDirectory(Path.Combine(root, "editions"));
        Directory.CreateDirectory(Path.Combine(root, "images", date.ToString("yyyy-MM-dd")));
    }

    public async Task<(OffworldNewsEditionDto? Edition, string? Error)> RegenerateTodayEditionAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return (null, "Offworld News is disabled in configuration.");
        }

        var date = UtcGameClock.Today;
        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            BackgroundUpgradeQueued.TryRemove(date, out _);
            DeleteEditionImages(date);

            var stale = TryLoadEdition(date);
            if (stale is not null)
            {
                TrySaveEdition(ResetGeneratedImagesToPlaceholders(stale));
            }

            var edition = await GenerateAndStoreEditionAsync(date, ct);
            logger.LogInformation("Admin regenerated Offworld News edition for {Date}", date);
            return (edition, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin Offworld News edition regeneration failed for {Date}", date);
            return (null, "Edition regeneration failed. Check API logs for details.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<(OffworldNewsEditionDto? Edition, string? Error)> RegenerateTodayImagesAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return (null, "Offworld News is disabled in configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return (null, "OffworldNews.ApiKey is not configured; AI images are unavailable.");
        }

        var date = UtcGameClock.Today;
        var existing = TryLoadEdition(date);
        if (existing is null || existing.Stories.Count == 0)
        {
            return (null, "No edition found for today. Regenerate stories first.");
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            EnsureCacheDirectories(date);
            DeleteEditionImages(date);

            existing = ResetGeneratedImagesToPlaceholders(existing);
            TrySaveEdition(existing);

            var generator = new OpenAiOffworldNewsGenerator(
                _options,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            var (edition, imageSummary) = await generator.RegenerateImagesAsync(existing, GetCacheRoot(), ct);
            edition = EnrichEditionAuthors(EnsureStoryImages(edition));
            TrySaveEdition(edition);

            var illustrated = CountIllustratedStories(edition);
            logger.LogInformation(
                "Admin regenerated Offworld News images for {Date}: {Succeeded}/{Attempted} illustrated",
                date,
                imageSummary.Succeeded,
                imageSummary.Attempted);

            if (illustrated == 0 && imageSummary.Attempted > 0)
            {
                return (null, imageSummary.DescribeFailure());
            }

            return (edition, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin Offworld News image regeneration failed for {Date}", date);
            return (null, "Image regeneration failed. Check API logs for details.");
        }
        finally
        {
            gate.Release();
        }
    }

    private static int CountIllustratedStories(OffworldNewsEditionDto edition) =>
        edition.Stories.Count(story =>
            !string.IsNullOrWhiteSpace(story.ImageUrl)
            && story.ImageUrl.Contains("/images/", StringComparison.OrdinalIgnoreCase));

    public static AdminOffworldNewsRegenerateResponse ToRegenerateResponse(
        OffworldNewsEditionDto edition,
        string message,
        OffworldNewsImageGenerationSummary? imageSummary = null)
    {
        var illustrated = CountIllustratedStories(edition);
        var attempts = imageSummary?.Attempted ?? illustrated;
        var error = imageSummary is { Succeeded: 0, Attempted: > 0 }
            ? imageSummary.DescribeFailure()
            : null;

        if (illustrated == 0 && string.IsNullOrWhiteSpace(error))
        {
            error = "No AI images were saved. Check OffworldNews settings and API logs.";
        }

        return new AdminOffworldNewsRegenerateResponse(
            message,
            edition.EditionDate,
            edition.Source,
            edition.Stories.Count,
            illustrated,
            attempts,
            error);
    }

    private void DeleteEditionImages(DateOnly date)
    {
        var imageDir = Path.Combine(GetCacheRoot(), "images", date.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(imageDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(imageDir))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Offworld News image {Path}", path);
            }
        }
    }
}
