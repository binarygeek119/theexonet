using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services.ExonetAiAssetScan;

public sealed class OffworldNewsReporterScanner : IExonetAiAssetScanner
{
    public string AreaName => "OffworldNewsReporters";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        if (!context.OffworldNewsEnabled)
        {
            return ExonetAiAssetScanAreaResult.SkippedArea(AreaName);
        }

        var reportersRoot = context.HostingPaths.OffworldNewsReportersAssetsRoot;
        var csvPath = context.OffworldNewsReportersCsvPath;

        IReadOnlyList<OffworldNewsReporterProfile> reporters;
        try
        {
            if (!File.Exists(csvPath))
            {
                return new ExonetAiAssetScanAreaResult(AreaName, Invalid: 1);
            }

            reporters = OffworldNewsReportersCsvLoader.LoadFromFile(csvPath);
        }
        catch (Exception)
        {
            return new ExonetAiAssetScanAreaResult(AreaName, Invalid: 1);
        }

        var knownSlugs = reporters
            .Select(reporter => reporter.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = 0;
        foreach (var reporter in reporters)
        {
            if (!File.Exists(OffworldNewsReporterPaths.AvatarFilePath(reportersRoot, reporter.Slug)))
            {
                missing++;
            }

            if (!File.Exists(OffworldNewsReporterPaths.BackgroundFilePath(reportersRoot, reporter.Slug)))
            {
                missing++;
            }
        }

        var orphans = 0;
        if (Directory.Exists(reportersRoot))
        {
            foreach (var folder in Directory.EnumerateDirectories(reportersRoot))
            {
                var slug = Path.GetFileName(folder);
                if (!knownSlugs.Contains(slug))
                {
                    orphans++;
                }
            }
        }

        return new ExonetAiAssetScanAreaResult(
            AreaName,
            Orphans: orphans,
            Missing: missing);
    }
}
