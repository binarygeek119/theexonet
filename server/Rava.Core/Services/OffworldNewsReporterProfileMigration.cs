using Microsoft.Extensions.Logging;
using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Services;

/// <summary>
/// Backfills extended reporter profile columns from the publish template without overwriting admin edits.
/// </summary>
public static class OffworldNewsReporterProfileMigration
{
    public const string MarkerFileName = ".reporter-profile-v3.complete";

    public static void RunIfNeeded(string contentRootPath, string reportersFileName, ILogger logger)
    {
        var dataPath = RavaDataPaths.ResolveFile(contentRootPath, reportersFileName);
        var templatePath = Path.Combine(contentRootPath, reportersFileName);
        if (!File.Exists(templatePath))
        {
            templatePath = dataPath;
        }

        if (!File.Exists(dataPath))
        {
            return;
        }

        var markerPath = Path.Combine(Path.GetDirectoryName(dataPath)!, MarkerFileName);
        if (File.Exists(markerPath) && OffworldNewsReportersCsvLoader.HasSpeciesColumn(dataPath))
        {
            return;
        }

        try
        {
            var dataReporters = OffworldNewsReportersCsvLoader.LoadFromFile(dataPath).ToDictionary(
                reporter => reporter.Slug,
                StringComparer.OrdinalIgnoreCase);
            var templateReporters = File.Exists(templatePath)
                ? OffworldNewsReportersCsvLoader.LoadFromFile(templatePath)
                : [];

            var merged = new List<OffworldNewsReporterProfile>();
            foreach (var template in templateReporters)
            {
                if (!dataReporters.TryGetValue(template.Slug, out var existing))
                {
                    merged.Add(template);
                    continue;
                }

                merged.Add(MergeReporter(existing, template));
                dataReporters.Remove(template.Slug);
            }

            foreach (var remaining in dataReporters.Values.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(remaining);
            }

            OffworldNewsReportersCsvLoader.SaveToFile(dataPath, merged);
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            OffworldNewsReporterCatalog.Reload();

            logger.LogInformation(
                "Offworld News reporter profile migration complete: {Count} reporters in {Path}",
                merged.Count,
                dataPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Offworld News reporter profile migration failed for {Path}", dataPath);
        }
    }

    public static OffworldNewsReporterProfile MergeReporter(
        OffworldNewsReporterProfile existing,
        OffworldNewsReporterProfile template) =>
        existing with
        {
            NotableLocations = MergeList(existing.NotableLocations, template.NotableLocations),
            NotableStories = MergeList(existing.NotableStories, template.NotableStories),
            Appearance = MergeAppearance(existing.Appearance, template.Appearance),
        };

    private static IReadOnlyList<string> MergeList(
        IReadOnlyList<string> existing,
        IReadOnlyList<string> template) =>
        existing.Count > 0 ? existing : template;

    private static ReporterAppearance MergeAppearance(ReporterAppearance existing, ReporterAppearance template) =>
        new(
            Pick(existing.Hair, template.Hair),
            Pick(existing.Eyes, template.Eyes),
            Pick(existing.Race, template.Race),
            Pick(existing.Build, template.Build),
            Pick(existing.FacialHair, template.FacialHair),
            Pick(existing.Makeup, template.Makeup),
            Pick(existing.DistinctiveFeatures, template.DistinctiveFeatures),
            PickSpecies(existing.Species, template.Species));

    private static string PickSpecies(string existing, string template)
    {
        if (ReporterSpecies.IsHuman(existing) && !ReporterSpecies.IsHuman(template))
        {
            return ReporterSpecies.Normalize(template);
        }

        return string.IsNullOrWhiteSpace(existing)
            ? ReporterSpecies.Normalize(template)
            : ReporterSpecies.Normalize(existing);
    }

    private static string Pick(string existing, string template) =>
        string.IsNullOrWhiteSpace(existing) ? template.Trim() : existing.Trim();
}
