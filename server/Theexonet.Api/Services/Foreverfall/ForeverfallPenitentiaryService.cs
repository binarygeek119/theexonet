using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;
using Theexonet.Core.Services.ExonetAiAssetScan;

namespace Theexonet.Api.Services.Foreverfall;

public sealed class ForeverfallPenitentiaryService(
    IOptions<ForeverfallOptions> foreverfallOptions,
    ForeverfallAdminSettingsStore adminSettings,
    OpenAiConnectionResolver openAi,
    TheexonetHostingPaths hostingPaths,
    IHttpClientFactory httpClientFactory,
    AiImageQueuePublisher aiImageQueuePublisher,
    ILogger<ForeverfallPenitentiaryService> logger)
{
    private static readonly ConcurrentDictionary<DateOnly, SemaphoreSlim> GenerationLocks = new();
    private static readonly TimeSpan IntakeJobTimeout = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly ForeverfallOptions _options = foreverfallOptions.Value;
    private readonly string _cacheRoot = hostingPaths.ForeverfallCacheRoot;

    private ForeverfallOptions GenerationOptions => adminSettings.ToGenerationOptions();

    public async Task<ForeverfallRosterDto> GetRosterAsync(DateOnly? intakeDate = null, CancellationToken ct = default)
    {
        var date = intakeDate ?? UtcGameClock.Today;

        if (!GenerationOptions.Enabled)
        {
            return BuildEmptyRoster(date);
        }

        var cached = TryLoadRoster(date);
        if (cached is not null)
        {
            return EnrichRosterImageUrls(cached);
        }

        return BuildEmptyRoster(date);
    }

    public ForeverfallArchivesDto ListArchives()
    {
        if (!GenerationOptions.Enabled || ForeverfallStoragePaths.CountRosterFiles(_cacheRoot) == 0)
        {
            return new ForeverfallArchivesDto([]);
        }

        var generation = GenerationOptions;
        var today = UtcGameClock.Today;
        var entries = new List<ForeverfallArchiveEntryDto>();

        foreach (var path in ForeverfallStoragePaths.EnumerateRosterFiles(_cacheRoot))
        {
            if (!ForeverfallStoragePaths.TryParseRosterDate(path, out var date))
            {
                continue;
            }

            if (ForeverfallStoragePaths.IsRosterExpired(date, today, generation.RetentionDays))
            {
                continue;
            }

            var sample = (string?)null;
            var intakeCount = 0;
            var maleCount = 0;
            var femaleCount = 0;
            try
            {
                var roster = JsonSerializer.Deserialize<ForeverfallRosterDto>(File.ReadAllText(path), JsonOptions);
                if (roster is not null)
                {
                    intakeCount = roster.IntakeCount;
                    maleCount = roster.MaleCount;
                    femaleCount = roster.FemaleCount;
                    sample = roster.MaleWing.FirstOrDefault()?.DisplayName
                        ?? roster.FemaleWing.FirstOrDefault()?.DisplayName;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read Foreverfall archive index for {Date}", date);
            }

            entries.Add(new ForeverfallArchiveEntryDto(date, intakeCount, maleCount, femaleCount, sample));
        }

        entries.Sort((left, right) => right.IntakeDate.CompareTo(left.IntakeDate));
        return new ForeverfallArchivesDto(entries);
    }

    public ForeverfallSearchResultDto SearchInmates(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ForeverfallSearchResultDto([], 0);
        }

        var normalized = query.Trim();
        var generation = GenerationOptions;
        var today = UtcGameClock.Today;
        var matches = new List<ForeverfallInmateDto>();

        foreach (var path in ForeverfallStoragePaths.EnumerateRosterFiles(_cacheRoot))
        {
            if (!ForeverfallStoragePaths.TryParseRosterDate(path, out var date))
            {
                continue;
            }

            if (ForeverfallStoragePaths.IsRosterExpired(date, today, generation.RetentionDays))
            {
                continue;
            }

            var roster = TryLoadRoster(date);
            if (roster is null)
            {
                continue;
            }

            foreach (var inmate in roster.MaleWing.Concat(roster.FemaleWing))
            {
                if (MatchesQuery(inmate, normalized))
                {
                    matches.Add(EnrichInmateImageUrl(inmate));
                }
            }
        }

        matches.Sort((left, right) => right.IntakeDate.CompareTo(left.IntakeDate));
        return new ForeverfallSearchResultDto(matches, matches.Count);
    }

    public ForeverfallInmateDto? TryGetInmate(string inmateId)
    {
        if (string.IsNullOrWhiteSpace(inmateId))
        {
            return null;
        }

        var generation = GenerationOptions;
        var today = UtcGameClock.Today;

        foreach (var path in ForeverfallStoragePaths.EnumerateRosterFiles(_cacheRoot))
        {
            if (!ForeverfallStoragePaths.TryParseRosterDate(path, out var date))
            {
                continue;
            }

            if (ForeverfallStoragePaths.IsRosterExpired(date, today, generation.RetentionDays))
            {
                continue;
            }

            var roster = TryLoadRoster(date);
            if (roster is null)
            {
                continue;
            }

            var match = roster.MaleWing.Concat(roster.FemaleWing)
                .FirstOrDefault(inmate => string.Equals(inmate.Id, inmateId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return EnrichInmateImageUrl(match);
            }
        }

        return null;
    }

    public AdminForeverfallStatusDto GetStatus()
    {
        var today = UtcGameClock.Today;
        var registry = LoadImageRegistry();
        var generation = GenerationOptions;
        DateOnly? oldest = null;
        var rosterCount = 0;

        foreach (var path in ForeverfallStoragePaths.EnumerateRosterFiles(_cacheRoot))
        {
            if (!ForeverfallStoragePaths.TryParseRosterDate(path, out var date))
            {
                continue;
            }

            if (ForeverfallStoragePaths.IsRosterExpired(date, today, generation.RetentionDays))
            {
                continue;
            }

            rosterCount++;
            oldest = oldest is null || date < oldest ? date : oldest;
        }

        var todayRoster = TryLoadRoster(today);
        return new AdminForeverfallStatusDto(
            today,
            todayRoster?.IntakeCount ?? 0,
            registry.Images.Count,
            generation.MaxInmateImages,
            oldest,
            rosterCount);
    }

    public int PurgeExpiredRosters()
    {
        var generation = GenerationOptions;
        var today = UtcGameClock.Today;
        var purged = 0;

        foreach (var path in ForeverfallStoragePaths.EnumerateRosterFiles(_cacheRoot).ToList())
        {
            if (!ForeverfallStoragePaths.TryParseRosterDate(path, out var date))
            {
                continue;
            }

            if (!ForeverfallStoragePaths.IsRosterExpired(date, today, generation.RetentionDays))
            {
                continue;
            }

            try
            {
                File.Delete(path);
                purged++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to purge Foreverfall roster {Path}", path);
            }
        }

        return purged;
    }

    public async Task EnsureDailyIntakeAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct = default)
    {
        if (!GenerationOptions.Enabled)
        {
            return;
        }

        ForeverfallStoragePaths.EnsureDirectories(_cacheRoot);

        if (!forceRegenerate)
        {
            var existing = TryLoadRoster(date);
            if (existing is not null)
            {
                if (!string.Equals(existing.Source, "openai", StringComparison.OrdinalIgnoreCase)
                    && openAi.IsApiKeyConfigured)
                {
                    await QueueIntakeUpgradeAsync(date, forceRegenerate: false, ct);
                }

                return;
            }
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRegenerate && TryLoadRoster(date) is not null)
            {
                return;
            }

            SaveTemplateRoster(date);

            logger.LogInformation(
                "Foreverfall template roster ready for {Date} (force={Force})",
                date,
                forceRegenerate);

            if (openAi.IsApiKeyConfigured)
            {
                await QueueIntakeUpgradeAsync(date, forceRegenerate, ct);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<(bool Ok, string? Error, int PortraitsQueued)> RegenerateIntakeAndWaitAsync(
        CancellationToken ct = default)
    {
        if (!GenerationOptions.Enabled)
        {
            return (false, "Foreverfall Penitentiary is disabled in configuration.", 0);
        }

        var date = UtcGameClock.Today;
        var source = $"foreverfall:intake:{date:yyyy-MM-dd}";
        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            SaveTemplateRoster(date);

            if (!openAi.IsApiKeyConfigured)
            {
                return (true, null, 0);
            }

            var enqueue = await aiImageQueuePublisher.EnqueueForeverfallIntakeAsync(
                date,
                forceRegenerate: true,
                source,
                ct);
            if (enqueue.EnqueuedCount == 0 && enqueue.Message is not null)
            {
                return (false, enqueue.Message, 0);
            }

            var wait = await aiImageQueuePublisher.WaitForJobAsync(
                AiImageJobKinds.ForeverfallIntake,
                source,
                IntakeJobTimeout,
                ct);
            if (wait.Failed)
            {
                return (false, wait.Error ?? "Foreverfall intake generation failed.", 0);
            }

            if (!wait.Completed)
            {
                return (false, wait.Error ?? "Timed out waiting for Foreverfall intake generation.", 0);
            }

            var portraitStatus = await aiImageQueuePublisher.GetStatusAsync(
                AiImageJobKinds.ForeverfallPortrait,
                ct);
            return (true, null, portraitStatus.QueuedCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin Foreverfall intake regeneration failed for {Date}", date);
            return (false, "Intake regeneration failed. Check API logs for details.", 0);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<(bool Ok, string? Error)> ProcessIntakeJobAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct)
    {
        if (!GenerationOptions.Enabled)
        {
            return (false, "Foreverfall Penitentiary is disabled in configuration.");
        }

        var gate = GenerationLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var cached = TryLoadRoster(date);
            if (!forceRegenerate
                && cached is not null
                && string.Equals(cached.Source, "openai", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null);
            }

            var pendingPortraits = await GenerateAndStoreRosterAsync(date, ct);
            if (pendingPortraits.Count > 0)
            {
                await aiImageQueuePublisher.EnqueueForeverfallPortraitsAsync(
                    pendingPortraits,
                    $"foreverfall:intake:{date:yyyy-MM-dd}",
                    ct);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Foreverfall intake job failed for {Date}", date);
            return (false, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task QueueIntakeUpgradeAsync(
        DateOnly date,
        bool forceRegenerate,
        CancellationToken ct)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return;
        }

        var source = $"foreverfall:intake:{date:yyyy-MM-dd}";
        var result = await aiImageQueuePublisher.EnqueueForeverfallIntakeAsync(
            date,
            forceRegenerate,
            source,
            ct);

        if (result.EnqueuedCount > 0)
        {
            logger.LogInformation(
                "Queued Foreverfall intake job for {Date} (force={Force})",
                date,
                forceRegenerate);
        }
    }

    public async Task<(bool Ok, string? Error)> GenerateAndSavePortraitAsync(
        ForeverfallPortraitJobItem item,
        CancellationToken ct = default)
    {
        if (!openAi.IsApiKeyConfigured)
        {
            return (false, "OpenAi.ApiKey is not configured.");
        }

        var portraitGenerator = new ForeverfallPortraitGenerator(
            openAi,
            httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
            _cacheRoot);

        var (ok, error) = await portraitGenerator.GenerateAndSaveAsync(
            item.ImageId,
            item.DisplayName,
            item.Species,
            item.Gender,
            ct);
        if (!ok)
        {
            return (false, error);
        }

        var registry = LoadImageRegistry();
        SaveImageRegistry(AppendRegistryEntry(registry, item.ImageId, item.Gender));
        return (true, null);
    }

    private void SaveTemplateRoster(DateOnly date)
    {
        var generation = GenerationOptions;
        var intakeCount = ForeverfallIntakeSelector.ResolveIntakeCount(date, generation);
        var (maleCount, femaleCount) = ForeverfallIntakeSelector.SplitByGender(intakeCount, date);
        var registry = LoadImageRegistry();
        var assignments = ForeverfallPortraitImageAssigner.Assign(
            date,
            maleCount,
            femaleCount,
            registry,
            generation.MaxInmateImages);
        var generated = ForeverfallInmateTemplateGenerator.Generate(date, intakeCount, maleCount, femaleCount);
        var roster = BuildRosterFromProfiles(date, assignments, generated, "template");
        TrySaveRoster(roster);
    }

    private async Task<IReadOnlyList<ForeverfallPortraitJobItem>> GenerateAndStoreRosterAsync(DateOnly date, CancellationToken ct)
    {
        var generation = GenerationOptions;
        var intakeCount = ForeverfallIntakeSelector.ResolveIntakeCount(date, generation);
        var (maleCount, femaleCount) = ForeverfallIntakeSelector.SplitByGender(intakeCount, date);
        var registry = LoadImageRegistry();
        var assignments = ForeverfallPortraitImageAssigner.Assign(
            date,
            maleCount,
            femaleCount,
            registry,
            generation.MaxInmateImages);

        IReadOnlyList<GeneratedForeverfallInmate> generated;
        string source;

        if (openAi.IsApiKeyConfigured)
        {
            var generator = new OpenAiForeverfallInmateGenerator(
                openAi,
                httpClientFactory.CreateClient(OpenAiOffworldNewsGenerator.HttpClientName),
                logger);
            generated = await generator.GenerateInmatesAsync(date, intakeCount, maleCount, femaleCount, ct);
            source = "openai";
        }
        else
        {
            generated = ForeverfallInmateTemplateGenerator.Generate(date, intakeCount, maleCount, femaleCount);
            source = "template";
        }

        var roster = BuildRosterFromProfiles(date, assignments, generated, source);
        var pendingPortraits = CollectPendingPortraits(assignments, generated);
        TrySaveRoster(roster);
        logger.LogInformation(
            "Foreverfall intake ready for {Date}: {Count} inmates ({Male} male, {Female} female), source={Source}, portraitsQueued={PortraitsQueued}",
            date,
            roster.IntakeCount,
            roster.MaleCount,
            roster.FemaleCount,
            source,
            pendingPortraits.Count);
        return pendingPortraits;
    }

    private ForeverfallRosterDto BuildRosterFromProfiles(
        DateOnly date,
        IReadOnlyList<ForeverfallPortraitAssignment> assignments,
        IReadOnlyList<GeneratedForeverfallInmate> generated,
        string source)
    {
        var inmates = new List<ForeverfallInmateDto>(assignments.Count);
        for (var index = 0; index < assignments.Count && index < generated.Count; index++)
        {
            var assignment = assignments[index];
            var profile = generated[index];
            var inmateId = $"FFP-{date:yyyyMMdd}-{index + 1:D3}";
            inmates.Add(new ForeverfallInmateDto(
                inmateId,
                date,
                profile.DisplayName,
                profile.Species,
                assignment.Gender,
                profile.Sentence,
                profile.Crime,
                profile.IntakeReason,
                profile.Bio,
                assignment.ImageId,
                ForeverfallStoragePaths.ResolvePublicImageUrl(_cacheRoot, assignment.ImageId)));
        }

        var maleWing = inmates.Where(inmate => inmate.Gender == "male").ToList();
        var femaleWing = inmates.Where(inmate => inmate.Gender == "female").ToList();
        return new ForeverfallRosterDto(
            date,
            DateTime.UtcNow,
            source,
            inmates.Count,
            maleWing.Count,
            femaleWing.Count,
            maleWing,
            femaleWing);
    }

    private List<ForeverfallPortraitJobItem> CollectPendingPortraits(
        IReadOnlyList<ForeverfallPortraitAssignment> assignments,
        IReadOnlyList<GeneratedForeverfallInmate> generated)
    {
        var pendingPortraits = new List<ForeverfallPortraitJobItem>();
        if (!openAi.IsApiKeyConfigured)
        {
            return pendingPortraits;
        }

        for (var index = 0; index < assignments.Count && index < generated.Count; index++)
        {
            var assignment = assignments[index];
            var profile = generated[index];
            if (assignment.NeedsGeneration)
            {
                pendingPortraits.Add(new ForeverfallPortraitJobItem(
                    assignment.ImageId,
                    profile.DisplayName,
                    profile.Species,
                    assignment.Gender));
            }
        }

        return pendingPortraits;
    }

    private static bool MatchesQuery(ForeverfallInmateDto inmate, string query)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return inmate.DisplayName.Contains(query, comparison)
            || inmate.Species.Contains(query, comparison)
            || inmate.Crime.Contains(query, comparison)
            || inmate.IntakeReason.Contains(query, comparison)
            || inmate.Bio.Contains(query, comparison)
            || inmate.Id.Contains(query, comparison);
    }

    private ForeverfallRosterDto BuildEmptyRoster(DateOnly date) =>
        new(date, DateTime.UtcNow, "empty", 0, 0, 0, [], []);

    private ForeverfallRosterDto EnrichRosterImageUrls(ForeverfallRosterDto roster) =>
        roster with
        {
            MaleWing = roster.MaleWing.Select(EnrichInmateImageUrl).ToList(),
            FemaleWing = roster.FemaleWing.Select(EnrichInmateImageUrl).ToList(),
        };

    private ForeverfallInmateDto EnrichInmateImageUrl(ForeverfallInmateDto inmate) =>
        inmate with
        {
            ImageUrl = ForeverfallStoragePaths.ResolvePublicImageUrl(_cacheRoot, inmate.ImageId),
        };

    private ForeverfallRosterDto? TryLoadRoster(DateOnly date)
    {
        var path = ForeverfallStoragePaths.ResolveRosterFilePath(_cacheRoot, date);
        if (path is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ForeverfallRosterDto>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Foreverfall roster cache for {Date}", date);
            return null;
        }
    }

    private void TrySaveRoster(ForeverfallRosterDto roster)
    {
        try
        {
            ForeverfallStoragePaths.EnsureDirectories(_cacheRoot);
            var path = ForeverfallStoragePaths.RosterFilePath(_cacheRoot, roster.IntakeDate);
            File.WriteAllText(path, JsonSerializer.Serialize(roster, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to write Foreverfall roster cache for {Date} under {Root}",
                roster.IntakeDate,
                _cacheRoot);
        }
    }

    private ForeverfallImageRegistry LoadImageRegistry()
    {
        var path = ForeverfallStoragePaths.ImageRegistryPath(_cacheRoot);
        if (!File.Exists(path))
        {
            return new ForeverfallImageRegistry([], 1);
        }

        try
        {
            return JsonSerializer.Deserialize<ForeverfallImageRegistry>(File.ReadAllText(path), JsonOptions)
                ?? new ForeverfallImageRegistry([], 1);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read Foreverfall image registry");
            return new ForeverfallImageRegistry([], 1);
        }
    }

    private void SaveImageRegistry(ForeverfallImageRegistry registry)
    {
        try
        {
            ForeverfallStoragePaths.EnsureDirectories(_cacheRoot);
            var path = ForeverfallStoragePaths.ImageRegistryPath(_cacheRoot);
            File.WriteAllText(path, JsonSerializer.Serialize(registry, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write Foreverfall image registry");
        }
    }

    private static ForeverfallImageRegistry AppendRegistryEntry(
        ForeverfallImageRegistry registry,
        string imageId,
        string gender)
    {
        if (registry.Images.Any(entry => string.Equals(entry.ImageId, imageId, StringComparison.Ordinal)))
        {
            return registry;
        }

        var entries = registry.Images.ToList();
        entries.Add(new ForeverfallImageRegistryEntry(
            imageId,
            gender,
            DateTime.UtcNow,
            $"{imageId}.jpg"));

        var nextNumber = registry.NextImageNumber;
        if (int.TryParse(imageId.AsSpan(3), out var parsed))
        {
            nextNumber = Math.Max(nextNumber, parsed + 1);
        }

        return new ForeverfallImageRegistry(entries, nextNumber);
    }

    public int GetPortraitPoolCount() => LoadImageRegistry().Images.Count;

    public ExonetAiAssetScanAreaResult SyncPortraitRegistryFromDisk() =>
        ForeverfallPortraitRegistryScanner.Sync(_cacheRoot);
}
