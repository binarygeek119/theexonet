using Theexonet.Core.Configuration;
using Theexonet.Core.Services;
using Theexonet.Core.Services.ExonetAiAssetScan;

namespace Theexonet.Core.Tests;

public class OffworldNewsReporterScannerTests
{
    [Fact]
    public void Scan_reports_missing_assets_for_csv_slug()
    {
        var root = CreateTempDirectory();
        var cacheRoot = Path.Combine(root, "offworld-news");
        var csvPath = Path.Combine(root, "reporters.csv");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "reporters"));

        try
        {
            OffworldNewsReportersCsvLoader.SaveToFile(csvPath,
            [
                SampleReporter("mira-solano"),
            ]);

            var result = Scan(cacheRoot, csvPath);

            Assert.Equal(0, result.Orphans);
            Assert.Equal(2, result.Missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_reports_orphan_reporter_folder_not_in_csv()
    {
        var root = CreateTempDirectory();
        var cacheRoot = Path.Combine(root, "offworld-news");
        var csvPath = Path.Combine(root, "reporters.csv");
        var reportersRoot = Path.Combine(cacheRoot, "reporters");
        Directory.CreateDirectory(reportersRoot);

        try
        {
            OffworldNewsReportersCsvLoader.SaveToFile(csvPath,
            [
                SampleReporter("mira-solano"),
            ]);

            Directory.CreateDirectory(OffworldNewsReporterPaths.ReporterFolder(reportersRoot, "orphan-slug"));

            var result = Scan(cacheRoot, csvPath);

            Assert.Equal(1, result.Orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ExonetAiAssetScanAreaResult Scan(string offworldNewsCacheRoot, string csvPath)
    {
        var hostingPaths = new TheexonetHostingPaths
        {
            DataRoot = Path.GetDirectoryName(offworldNewsCacheRoot)!,
            ImagesRoot = offworldNewsCacheRoot,
            OffworldNewsCacheRoot = offworldNewsCacheRoot,
            LunarWeatherCacheRoot = offworldNewsCacheRoot,
            ForeverfallCacheRoot = offworldNewsCacheRoot,
            VoidCorpCacheRoot = offworldNewsCacheRoot,
            WebRoot = offworldNewsCacheRoot,
        };

        var context = new ExonetAiAssetScanContext(
            hostingPaths,
            csvPath,
            ForeverfallEnabled: false,
            OffworldNewsEnabled: true,
            LunarWeatherEnabled: false,
            VoidCorpEnabled: false,
            SupplyItems: []);

        return new OffworldNewsReporterScanner().Scan(context);
    }

    private static OffworldNewsReporterProfile SampleReporter(string slug) =>
        new(
            slug,
            "Display Name",
            "Title",
            "Beat",
            "Bureau",
            "Personality",
            "Voice",
            "Directory bio",
            "ONN bio",
            "Kicker",
            ["specialty"]);

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"onn-rep-scan-{Guid.NewGuid():N}");
}
