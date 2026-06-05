using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Core.Services;

namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed class VoidCorpCatalogScanner : IExonetAiAssetScanner
{
    public string AreaName => "VoidCorp";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        if (!context.VoidCorpEnabled)
        {
            return ExonetAiAssetScanAreaResult.SkippedArea(AreaName);
        }

        return Sync(context.HostingPaths.VoidCorpCacheRoot, context.SupplyItems);
    }

    public static ExonetAiAssetScanAreaResult Sync(
        string cacheRoot,
        IReadOnlyList<TradeItemDefinition> supplies)
    {
        VoidCorpStoragePaths.EnsureDirectories(cacheRoot);

        var syncResult = VoidCorpCatalogSync.Sync(cacheRoot, supplies);
        var catalog = VoidCorpCatalogSync.Load(cacheRoot);
        var referencedSlugs = catalog.Products
            .Select(entry => entry.Slug)
            .ToHashSet(StringComparer.Ordinal);

        var imagesDir = Path.Combine(cacheRoot, VoidCorpStoragePaths.ImagesFolder);
        var orphans = 0;
        if (Directory.Exists(imagesDir))
        {
            foreach (var imagePath in Directory.EnumerateFiles(imagesDir, "*.jpg", SearchOption.TopDirectoryOnly))
            {
                var slug = Path.GetFileNameWithoutExtension(imagePath);
                if (!referencedSlugs.Contains(slug))
                {
                    orphans++;
                }
            }
        }

        return new ExonetAiAssetScanAreaResult(
            "VoidCorp",
            Imported: syncResult.Added,
            AlreadyRegistered: syncResult.Unchanged,
            SkippedInvalid: syncResult.Updated,
            Orphans: orphans,
            Missing: syncResult.MissingImages);
    }
}
