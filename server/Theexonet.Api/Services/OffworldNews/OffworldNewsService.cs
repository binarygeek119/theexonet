using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;
using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Core.Constants;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.OffworldNews;

public sealed class OffworldNewsService(
    IOptions<OffworldNewsOptions> options,
    OffworldNewsAdminSettingsStore adminSettings,
    OpenAiConnectionResolver openAi,
    TheexonetHostingPaths hostingPaths,
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    AiImageQueuePublisher aiImageQueuePublisher,
    ILogger<OffworldNewsService> logger)
{
    private static readonly ConcurrentDictionary<DateOnly, SemaphoreSlim> GenerationLocks = new();
    private static readonly TimeSpan EditionStoriesJobTimeout = TimeSpan.FromMinutes(10);
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
            return OffworldNewsTemplateGenerator.Generate(date, ResolveStoryCount(date));
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
            OffworldNewsTemplateGenerator.Generate(date, ResolveStoryCount(date), companyContext));
    }

    public OffworldNewsArchivesDto ListArchives()
    {
        if (!_options.Enabled)
        {
            return new OffworldNewsArchivesDto([]);
        }

        if (OffworldNewsStoragePaths.CountEditionFiles(GetCacheRoot()) == 0)
        {
            return new OffworldNewsArchivesDto([]);
        }

        var entries = new List<OffworldNewsArchiveEntryDto>();
        foreach (var path in OffworldNewsStoragePaths.EnumerateEditionFiles(GetCacheRoot()))
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

    public PublicOpenAiExonetSnapshotDto GetPublicAiSnapshot(
        int reporterPoolSize,
        int activeReporterPool,
        AdminAiImageQueueStatusDto portraitJob)
    {
        var today = UtcGameClock.Today;
        var edition = TryLoadEdition(today);
        var archiveCount = OffworldNewsStoragePaths.CountEditionFiles(GetCacheRoot());

        int? illustrated = null;
        if (edition?.Stories is { Count: > 0 })
        {
            illustrated = edition.Stories.Count(story =>
                !string.IsNullOrWhiteSpace(story.ImageUrl)
                && story.ImageUrl.Contains("/images/", StringComparison.OrdinalIgnoreCase));
        }

        return new PublicOpenAiExonetSnapshotDto(
            _options.Enabled,
            edition is not null ? today : (DateOnly?)null,
            edition?.Source,
            edition?.Stories?.Count,
            illustrated,
            reporterPoolSize,
            activeReporterPool,
            OffworldNewsReporterCatalog.All.Count,
            archiveCount,
            portraitJob.Status ?? "idle",
            portraitJob.CurrentJobDescription,
            portraitJob.CompletedToday,
            portraitJob.CompletedToday + portraitJob.FailedToday);
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

        if (!openAi.IsApiKeyConfigured)
        {
            return (null, "OpenAi.ApiKey is not configured.");
        }

        if (slugs is { Count: > 0 } && slugs.All(slug => OffworldNewsReporterCatalog.TryGetBySlug(slug) is null))
        {
            return (null, "Reporter not found.");
        }

        var result = await aiImageQueuePublisher.EnqueueOnnReporterPortraitsAsync(
            slugs,
            assets,
            "admin:reporter-portraits",
            ct);
        if (result.EnqueuedCount == 0)
        {
            return (null, result.Message ?? "No reporter portrait jobs were queued.");
        }

        var reporterCount = slugs is { Count: > 0 }
            ? slugs.Count(slug => OffworldNewsReporterCatalog.TryGetBySlug(slug) is not null)
            : OffworldNewsReporterCatalog.All.Count();
        var imageAttempts = assets switch
        {
            ReporterPortraitAssetKind.Avatar => reporterCount,
            ReporterPortraitAssetKind.Background => reporterCount,
            _ => reporterCount * 2,
        };

        return (
            new OffworldNewsReporterPortraitGenerationSummary(
                reporterCount,
                imageAttempts,
                0,
                null),
            null);
    }

    public async Task<(bool Ok, string? Error)> ApplyQueuedStoryImageAsync(
        DateOnly editionDate,
        string storyId,
        int storyIndex,
        string? imagePrompt,
        CancellationToken ct)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        var edition = TryLoadEdition(editionDate);
        if (edition is null || edition.Stories.Count == 0)
        {
            return (false, $"No Offworld News edition found for {editionDate:yyyy-MM-dd}.");
        }

        var index = -1;
        if (storyIndex >= 0 && storyIndex < edition.Stories.Count)
        {
            index = storyIndex;
        }
        else
        {
            for (var candidateIndex = 0; candidateIndex < edition.Stories.Count; candidateIndex++)
            {
                if (string.Equals(edition.Stories[candidateIndex].Id, storyId, StringComparison.OrdinalIgnoreCase))
                {
                    index = candidateIndex;
                    break;
                }
            }
        }

        if (index < 0)
        {
            return (false, $"Story {storyId} not found in edition {editionDate:yyyy-MM-dd}.");
        }

        var story = edition.Stories[index];
        var generator = new OpenAiOffworldNewsGenerator(
            GenerationOptions,
            _options,
            openAi,
            httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
            logger);
        var (ok, error, imageUrl, imageAspect) = await generator.GenerateStoryImageAsync(
            story,
            imagePrompt,
            editionDate,
            index,
            GetCacheRoot(),
            ct);

        var stories = edition.Stories.ToList();
        if (ok && !string.IsNullOrWhiteSpace(imageUrl))
        {
            stories[index] = story with { ImageUrl = imageUrl, ImageAspect = imageAspect };
        }
        else
        {
            stories[index] = story with
            {
                ImageUrl = story.ImageUrl
                    ?? OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
            };
        }

        var updated = edition with
        {
            GeneratedAt = DateTime.UtcNow,
            Stories = stories,
        };
        TrySaveEdition(EnrichEditionAuthors(EnsureStoryImages(updated)));
        return ok ? (true, null) : (false, error ?? "Story image generation failed.");
    }

    private IReadOnlyList<OffworldNewsReporterStoryRefDto> ListStoriesByReporter(string displayName, int limit)
    {
        limit = Math.Clamp(limit, 1, 50);
        var matches = new List<OffworldNewsReporterStoryRefDto>();
        if (OffworldNewsStoragePaths.CountEditionFiles(GetCacheRoot()) == 0)
        {
            return matches;
        }

        foreach (var path in OffworldNewsStoragePaths.EnumerateEditionFiles(GetCacheRoot()).OrderByDescending(File.GetLastWriteTimeUtc))
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

        OffworldNewsStoragePaths.EnsureEditionDirectories(GetCacheRoot(), date);

        if (!forceRegenerate)
        {
            var existing = TryLoadEdition(date);
            if (existing is not null)
            {
                if (!string.Equals(existing.Source, "openai", StringComparison.OrdinalIgnoreCase)
                    && openAi.IsApiKeyConfigured)
                {
                    await QueueEditionStoriesUpgradeAsync(date, forceRegenerate: false, ct);
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

            var companyContext = await LoadCompanyContextAsync(ct);
            var template = OffworldNewsTemplateGenerator.Generate(date, ResolveStoryCount(date), companyContext);
            TrySaveEdition(template);

            logger.LogInformation(
                "Offworld News template edition ready for {Date} (force={Force})",
                date,
                forceRegenerate);

            if (openAi.IsApiKeyConfigured)
            {
                await QueueEditionStoriesUpgradeAsync(date, forceRegenerate, ct);
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

    private static OffworldNewsEditionDto EnrichEditionAuthors(OffworldNewsEditionDto edition) =>
        OffworldNewsEditionEnricher.EnrichAuthors(edition);

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
                ImageUrl = OffworldNewsImagePaths.LostTransmissionImagePath,
                ImageAspect = story.ImageAspect,
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
        var path = OffworldNewsStoragePaths.ResolveEditionFilePath(GetCacheRoot(), date);
        if (path is null)
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
        if (!openAi.IsApiKeyConfigured)
        {
            logger.LogInformation(
                "OpenAi.ApiKey not configured; using template edition for {Date}",
                date);
            edition = OffworldNewsTemplateGenerator.Generate(date, ResolveStoryCount(date), companyContext);
        }
        else
        {
            var generator = new OpenAiOffworldNewsGenerator(
                GenerationOptions,
                _options,
                openAi,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            var (generatedEdition, imageJobs) = await generator.GenerateEditionWithoutImagesAsync(
                date,
                companyContext,
                ct);
            edition = generatedEdition;
            if (imageJobs.Count > 0)
            {
                await aiImageQueuePublisher.EnqueueOnnStoryImagesAsync(
                    date,
                    imageJobs,
                    $"onn:edition:{date:yyyy-MM-dd}",
                    ct);
            }
        }

        edition = EnrichEditionAuthors(EnsureStoryImages(edition));
        TrySaveEdition(edition);
        return edition;
    }

    private async Task QueueEditionStoriesUpgradeAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return;
        }

        var source = $"onn:edition:{date:yyyy-MM-dd}";
        var result = await aiImageQueuePublisher.EnqueueOnnEditionStoriesAsync(
            date,
            forceRegenerate,
            source,
            ct);

        if (result.EnqueuedCount > 0)
        {
            logger.LogInformation(
                "Queued Offworld News edition stories job for {Date} (force={Force})",
                date,
                forceRegenerate);
        }
    }

    public async Task<(bool Ok, string? Error)> ProcessEditionStoriesJobAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return (false, "Offworld News is disabled in configuration.");
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var cached = TryLoadEdition(date);
            if (!forceRegenerate
                && cached is not null
                && string.Equals(cached.Source, "openai", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null);
            }

            await GenerateAndStoreEditionAsync(date, ct);
            logger.LogInformation("Offworld News AI edition ready for {Date}", date);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Offworld News edition stories job failed for {Date}", date);
            return (false, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private void TrySaveEdition(OffworldNewsEditionDto edition)
    {
        try
        {
            OffworldNewsStoragePaths.EnsureEditionDirectories(GetCacheRoot(), edition.EditionDate);
            var path = OffworldNewsStoragePaths.EditionFilePath(GetCacheRoot(), edition.EditionDate);
            var json = JsonSerializer.Serialize(edition, JsonOptions);
            File.WriteAllText(path, json);
            OffworldNewsStoragePaths.DeleteLegacyEditionFile(GetCacheRoot(), edition.EditionDate);
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

    private OffworldNewsOptions GenerationOptions => adminSettings.ToGenerationOptions();

    private int ResolveStoryCount(DateOnly editionDate) =>
        OffworldNewsStoryCountSelector.ResolveStoryCount(editionDate, GenerationOptions);

    public async Task<(OffworldNewsEditionDto? Edition, string? Error)> RegenerateTodayEditionAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return (null, "Offworld News is disabled in configuration.");
        }

        var date = UtcGameClock.Today;
        var source = $"onn:edition:{date:yyyy-MM-dd}";
        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            DeleteEditionImages(date);

            var stale = TryLoadEdition(date);
            if (stale is not null)
            {
                TrySaveEdition(ResetGeneratedImagesToPlaceholders(stale));
            }
            else
            {
                var companyContext = await LoadCompanyContextAsync(ct);
                var template = OffworldNewsTemplateGenerator.Generate(date, ResolveStoryCount(date), companyContext);
                TrySaveEdition(template);
            }

            if (!openAi.IsApiKeyConfigured)
            {
                var templateEdition = TryLoadEdition(date);
                return (templateEdition, null);
            }

            var enqueue = await aiImageQueuePublisher.EnqueueOnnEditionStoriesAsync(
                date,
                forceRegenerate: true,
                source,
                ct);
            if (enqueue.EnqueuedCount == 0 && enqueue.Message is not null)
            {
                return (null, enqueue.Message);
            }

            var wait = await aiImageQueuePublisher.WaitForJobAsync(
                AiImageJobKinds.OnnEditionStories,
                source,
                EditionStoriesJobTimeout,
                ct);
            if (wait.Failed)
            {
                return (null, wait.Error ?? "Edition stories generation failed.");
            }

            if (!wait.Completed)
            {
                return (null, wait.Error ?? "Timed out waiting for edition stories generation.");
            }

            var edition = TryLoadEdition(date);
            if (edition is null)
            {
                return (null, "Edition regeneration completed but edition file is missing.");
            }

            logger.LogInformation("Admin regenerated Offworld News edition for {Date}", date);
            return (EnrichEditionAuthors(EnsureStoryImages(edition)), null);
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

        if (!openAi.IsApiKeyConfigured)
        {
            return (null, "OpenAi.ApiKey is not configured; AI images are unavailable.");
        }

        var date = UtcGameClock.Today;
        var existing = TryLoadEdition(date);
        if (existing is null || existing.Stories.Count == 0)
        {
            return (null, "No edition found for today. Regenerate stories first.");
        }

        if (existing.EditionDate != date)
        {
            return (null, "Today's edition file has a mismatched date. Regenerate stories first.");
        }

        var storyIndices = existing.Stories
            .Select((story, index) => (story, index))
            .Where(item => OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(item.story.ImageUrl, date))
            .Select(item => item.index)
            .ToList();

        if (storyIndices.Count == 0)
        {
            return (null, "No AI images found for today's edition.");
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            DeleteEditionImagesForStories(existing, date);
            existing = ResetGeneratedImagesToPlaceholdersForEdition(existing, date);
            TrySaveEdition(existing);

            var imageJobs = storyIndices
                .Select(index => (existing.Stories[index].Id, index, (string?)null))
                .ToList();
            await aiImageQueuePublisher.EnqueueOnnStoryImagesAsync(
                date,
                imageJobs,
                "admin:regenerate-images",
                ct);

            logger.LogInformation(
                "Admin queued Offworld News image regeneration for {Date}: {Count} story image job(s)",
                date,
                imageJobs.Count);

            return (EnrichEditionAuthors(EnsureStoryImages(existing)), null);
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

    private OffworldNewsEditionDto ResetGeneratedImagesToPlaceholdersForEdition(
        OffworldNewsEditionDto edition,
        DateOnly editionDate)
    {
        var stories = edition.Stories
            .Select(story => OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(story.ImageUrl, editionDate)
                ? story with
                {
                    ImageUrl = OffworldNewsTemplateGenerator.PlaceholderImageForCategory(story.Category),
                    ImageAspect = null,
                }
                : story)
            .ToList();

        return edition with { Stories = stories };
    }

    private void DeleteEditionImagesForStories(OffworldNewsEditionDto edition, DateOnly date)
    {
        foreach (var story in edition.Stories)
        {
            if (!OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(story.ImageUrl, date))
            {
                continue;
            }

            var path = OffworldNewsImagePaths.TryResolveCacheFilePath(GetCacheRoot(), story.ImageUrl);
            if (path is null)
            {
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Offworld News image {Path}", path);
            }
        }
    }

    private void DeleteEditionImages(DateOnly date) =>
        OffworldNewsStoragePaths.DeleteEditionImageDirectories(GetCacheRoot(), date);
}
