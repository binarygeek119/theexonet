using Theexonet.Core.Configuration;
using Theexonet.Core.Enums;
using Theexonet.Core.Models;
using Theexonet.Core.Services;
using Theexonet.Core.Services.ExonetAiAssetScan;

namespace Theexonet.Core.Tests;

public class VoidCorpCatalogScannerTests
{
    [Fact]
    public void Scan_adds_catalog_entries_and_counts_missing_images()
    {
        var root = CreateTempDirectory();
        try
        {
            var supplies = new List<TradeItemDefinition>
            {
                new()
                {
                    Category = ItemCategory.Supply,
                    ItemType = SupplyType.DrillBits.ToString(),
                    DisplayName = "Drill Bits",
                    BasePrice = 85m,
                    Color = "#b38033",
                    UiSymbol = "XLI",
                },
            };

            var result = VoidCorpCatalogScanner.Sync(root, supplies);

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Missing);
            Assert.True(File.Exists(VoidCorpStoragePaths.CatalogPath(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_counts_orphan_product_images()
    {
        var root = CreateTempDirectory();
        try
        {
            VoidCorpStoragePaths.EnsureDirectories(root);
            VoidCorpCatalogSync.Sync(root, []);
            File.WriteAllText(VoidCorpStoragePaths.ImageFilePath(root, "orphan"), "image");

            var result = VoidCorpCatalogScanner.Sync(root, []);

            Assert.Equal(1, result.Orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_skips_when_voidcorp_disabled()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(root);

        var hostingPaths = new TheexonetHostingPaths
        {
            DataRoot = root,
            ImagesRoot = root,
            OffworldNewsCacheRoot = root,
            LunarWeatherCacheRoot = root,
            ForeverfallCacheRoot = root,
            VoidCorpCacheRoot = root,
            WebRoot = root,
        };

        try
        {
            var context = new ExonetAiAssetScanContext(
                hostingPaths,
                OffworldNewsReportersCsvPath: string.Empty,
                ForeverfallEnabled: false,
                OffworldNewsEnabled: false,
                LunarWeatherEnabled: false,
                VoidCorpEnabled: false,
                SupplyItems: []);

            var result = new VoidCorpCatalogScanner().Scan(context);

            Assert.True(result.Skipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"voidcorp-scan-{Guid.NewGuid():N}");
}
