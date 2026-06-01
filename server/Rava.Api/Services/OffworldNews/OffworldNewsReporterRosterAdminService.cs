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
        var updated = new OffworldNewsReporterProfile(
            newSlug,
            displayName,
            request.Title.Trim(),
            request.Beat.Trim(),
            request.Bureau.Trim(),
            request.Personality.Trim(),
            request.WritingVoice.Trim(),
            request.DirectoryBio.Trim(),
            request.OnnBio.Trim(),
            request.StoryKicker.Trim(),
            ParseSpecialties(request.Specialties));

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

    public (AdminOffworldNewsSettingsDto? Settings, string? Error) SaveSettings(int reporterPoolSize)
    {
        var (poolSize, error) = settingsStore.SaveReporterPoolSize(reporterPoolSize);
        if (error is not null)
        {
            return (null, error);
        }

        _ = poolSize;
        return (BuildSettingsDto(), null);
    }

    private AdminOffworldNewsSettingsDto BuildSettingsDto()
    {
        var total = OffworldNewsReporterCatalog.All.Count;
        return new AdminOffworldNewsSettingsDto(
            settingsStore.ReporterPoolSize,
            total,
            settingsStore.ActivePoolCount());
    }

    private static AdminOffworldNewsReporterRowDto ToAdminRow(
        OffworldNewsReporterProfile reporter,
        bool inStoryPool) =>
        new(
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
            inStoryPool,
            OffworldNewsReporterPaths.AvatarUrl(reporter.Slug),
            OffworldNewsReporterPaths.BackgroundUrl(reporter.Slug));

    private static IReadOnlyList<string> ParseSpecialties(string? specialties) =>
        string.IsNullOrWhiteSpace(specialties)
            ? []
            : specialties
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(entry => entry.Length > 0)
                .ToList();

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
        var root = hostingPaths.OffworldNewsReportersAssetsRoot;
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
