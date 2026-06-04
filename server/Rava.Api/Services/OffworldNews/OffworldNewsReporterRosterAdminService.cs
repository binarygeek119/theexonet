using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;
using Rava.Infrastructure.Services;

namespace Rava.Api.Services.OffworldNews;

public sealed class OffworldNewsReporterRosterAdminService(
    IWebHostEnvironment environment,
    IOptionsMonitor<OffworldNewsOptions> optionsMonitor,
    OffworldNewsAdminSettingsStore settingsStore,
    RavaHostingPaths hostingPaths,
    ReporterFriendshipService reporterFriendships,
    ILogger<OffworldNewsReporterRosterAdminService> logger)
{
    public string ReportersFilePath =>
        RavaDataPaths.ResolveFile(environment.ContentRootPath, optionsMonitor.CurrentValue.ReportersFile);

    public AdminOffworldNewsReportersPageDto GetPage()
    {
        var reporters = OffworldNewsReporterCatalog.All;
        var poolSize = settingsStore.ReporterPoolSize;
        var rows = new List<AdminOffworldNewsReporterRowDto>();
        for (var index = 0; index < reporters.Count; index++)
        {
            var reporter = reporters[index];
            var inPool = poolSize <= 0 || poolSize >= reporters.Count || index < poolSize;
            rows.Add(ToAdminRow(reporter, inPool));
        }

        return new AdminOffworldNewsReportersPageDto(
            rows,
            BuildSettingsDto(),
            ReportersFilePath);
    }

    public async Task<(AdminOffworldNewsReporterRowDto? Reporter, string? Error)> AddReporterAsync(
        AdminCreateOffworldNewsReporterRequest request,
        CancellationToken ct)
    {
        _ = ct;
        var slug = NormalizeSlug(request.Slug);
        if (slug.Length == 0)
        {
            return (null, "Slug is invalid.");
        }

        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            return (null, "Display name is required.");
        }

        var reporters = OffworldNewsReportersCsvLoader.LoadFromFile(ReportersFilePath).ToList();
        if (reporters.Any(r => r.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)))
        {
            return (null, $"Slug '{slug}' is already used by another reporter.");
        }

        var created = BuildProfile(slug, request);
        reporters.Add(created);
        OffworldNewsReportersCsvLoader.SaveToFile(ReportersFilePath, reporters);
        OffworldNewsReporterCatalog.Reload();

        var poolSize = settingsStore.ReporterPoolSize;
        var index = reporters.Count - 1;
        var inPool = poolSize <= 0 || poolSize >= reporters.Count || index < poolSize;
        return (ToAdminRow(created, inPool), null);
    }

    public async Task<(AdminOffworldNewsReporterRowDto? Reporter, string? Error)> UpdateReporterAsync(
        string slug,
        AdminUpdateOffworldNewsReporterRequest request,
        CancellationToken ct)
    {
        var reporters = OffworldNewsReportersCsvLoader.LoadFromFile(ReportersFilePath).ToList();
        var index = reporters.FindIndex(r => r.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return (null, "Reporter not found.");
        }

        var displayName = request.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            return (null, "Display name is required.");
        }

        var newSlug = string.IsNullOrWhiteSpace(request.NewSlug)
            ? reporters[index].Slug
            : NormalizeSlug(request.NewSlug);
        if (newSlug.Length == 0)
        {
            return (null, "Slug is invalid.");
        }

        if (!newSlug.Equals(reporters[index].Slug, StringComparison.OrdinalIgnoreCase)
            && reporters.Any(r => r.Slug.Equals(newSlug, StringComparison.OrdinalIgnoreCase)))
        {
            return (null, $"Slug '{newSlug}' is already used by another reporter.");
        }

        var oldSlug = reporters[index].Slug;
        var updated = BuildProfile(
            newSlug,
            request);

        reporters[index] = updated;
        OffworldNewsReportersCsvLoader.SaveToFile(ReportersFilePath, reporters);
        OffworldNewsReporterCatalog.Reload();

        if (!oldSlug.Equals(newSlug, StringComparison.OrdinalIgnoreCase))
        {
            RenameReporterAssets(oldSlug, newSlug);
            var migrated = await reporterFriendships.MigrateReporterSlugAsync(oldSlug, newSlug, ct);
            logger.LogInformation(
                "Renamed ONN reporter slug {OldSlug} to {NewSlug}; migrated {FriendshipCount} friendships.",
                oldSlug,
                newSlug,
                migrated);
        }

        var poolSize = settingsStore.ReporterPoolSize;
        var inPool = poolSize <= 0 || poolSize >= reporters.Count || index < poolSize;
        return (ToAdminRow(updated, inPool), null);
    }

    public (AdminOffworldNewsSettingsDto? Settings, string? Error) SaveSettings(
        AdminUpdateOffworldNewsSettingsRequest request)
    {
        var (settings, error) = settingsStore.Save(request);
        return error is not null ? (null, error) : (settings, null);
    }

    private AdminOffworldNewsSettingsDto BuildSettingsDto() => settingsStore.GetSettings();

    private AdminOffworldNewsReporterRowDto ToAdminRow(
        OffworldNewsReporterProfile reporter,
        bool inStoryPool)
    {
        var assetRoots = hostingPaths.ReporterAssetRoots();
        return new(
            reporter.Slug,
            reporter.DisplayName,
            reporter.Title,
            reporter.Beat,
            reporter.Bureau,
            reporter.Personality,
            reporter.WritingVoice,
            reporter.DirectoryBio,
            reporter.OnnBio,
            reporter.StoryKicker,
            reporter.Specialties,
            reporter.Gender,
            string.Join("; ", reporter.NotableLocations),
            string.Join("; ", reporter.NotableStories),
            reporter.Appearance.Hair,
            reporter.Appearance.Eyes,
            reporter.Appearance.Race,
            reporter.Appearance.Build,
            reporter.Appearance.FacialHair,
            reporter.Appearance.Makeup,
            reporter.Appearance.DistinctiveFeatures,
            reporter.Appearance.Species,
            inStoryPool,
            OffworldNewsReporterPaths.ResolveAvatarUrl(reporter.Slug, assetRoots),
            OffworldNewsReporterPaths.ResolveBackgroundUrl(reporter.Slug, assetRoots));
    }

    private static OffworldNewsReporterProfile BuildProfile(
        string slug,
        AdminCreateOffworldNewsReporterRequest request) =>
        BuildProfile(
            slug,
            request.DisplayName,
            request.Title,
            request.Beat,
            request.Bureau,
            request.Personality,
            request.WritingVoice,
            request.DirectoryBio,
            request.OnnBio,
            request.StoryKicker,
            request.Specialties,
            request.Gender,
            request.NotableLocations,
            request.NotableStories,
            request.Hair,
            request.Eyes,
            request.Race,
            request.Build,
            request.FacialHair,
            request.Makeup,
            request.DistinctiveFeatures,
            request.Species);

    private static OffworldNewsReporterProfile BuildProfile(
        string slug,
        AdminUpdateOffworldNewsReporterRequest request) =>
        BuildProfile(
            slug,
            request.DisplayName,
            request.Title,
            request.Beat,
            request.Bureau,
            request.Personality,
            request.WritingVoice,
            request.DirectoryBio,
            request.OnnBio,
            request.StoryKicker,
            request.Specialties,
            request.Gender,
            request.NotableLocations,
            request.NotableStories,
            request.Hair,
            request.Eyes,
            request.Race,
            request.Build,
            request.FacialHair,
            request.Makeup,
            request.DistinctiveFeatures,
            request.Species);

    private static OffworldNewsReporterProfile BuildProfile(
        string slug,
        string displayName,
        string title,
        string beat,
        string bureau,
        string personality,
        string writingVoice,
        string directoryBio,
        string onnBio,
        string storyKicker,
        string specialties,
        string gender,
        string notableLocations,
        string notableStories,
        string hair,
        string eyes,
        string race,
        string build,
        string facialHair,
        string makeup,
        string distinctiveFeatures,
        string species) =>
        new(
            slug,
            displayName.Trim(),
            title.Trim(),
            beat.Trim(),
            bureau.Trim(),
            personality.Trim(),
            writingVoice.Trim(),
            directoryBio.Trim(),
            onnBio.Trim(),
            storyKicker.Trim(),
            ParseSpecialties(specialties),
            NormalizeGender(gender, slug),
            OffworldNewsReportersCsvLoader.ParseDelimitedList(notableLocations),
            OffworldNewsReportersCsvLoader.ParseDelimitedList(notableStories),
            new ReporterAppearance(
                hair,
                eyes,
                race,
                build,
                facialHair,
                makeup,
                distinctiveFeatures,
                ReporterSpecies.Normalize(species)));

    private static IReadOnlyList<string> ParseSpecialties(string? specialties) =>
        string.IsNullOrWhiteSpace(specialties)
            ? []
            : specialties
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(entry => entry.Length > 0)
                .ToList();

    private static string NormalizeGender(string? gender, string slug)
    {
        var normalized = OffworldNewsReporterPortraitGender.Normalize(gender);
        return normalized.Length > 0
            ? normalized
            : OffworldNewsReporterPortraitGender.InferForSlug(slug);
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        foreach (var ch in normalized)
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
            {
                continue;
            }

            return string.Empty;
        }

        return normalized.Trim('-');
    }

    private void RenameReporterAssets(string oldSlug, string newSlug)
    {
        foreach (var root in hostingPaths.ReporterAssetRoots())
        {
            RenameReporterAssetsInRoot(root, oldSlug, newSlug);
        }
    }

    private void RenameReporterAssetsInRoot(string root, string oldSlug, string newSlug)
    {
        var oldFolder = OffworldNewsReporterPaths.ReporterFolder(root, oldSlug);
        var newFolder = OffworldNewsReporterPaths.ReporterFolder(root, newSlug);
        if (!Directory.Exists(oldFolder))
        {
            return;
        }

        try
        {
            if (Directory.Exists(newFolder))
            {
                foreach (var file in Directory.GetFiles(oldFolder))
                {
                    var name = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(newFolder, name), overwrite: true);
                }

                Directory.Delete(oldFolder, recursive: true);
            }
            else
            {
                Directory.Move(oldFolder, newFolder);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not rename reporter asset folder from {OldSlug} to {NewSlug}.",
                oldSlug,
                newSlug);
        }
    }
}
