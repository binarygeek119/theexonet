using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.VoidCorp;

public sealed class VoidCorpCatalogService(
    TheexonetHostingPaths hostingPaths,
    ITradeItemsCatalog tradeItemsCatalog,
    VoidCorpMissingImageBackfillService backfillService,
    IOptions<VoidCorpOptions> voidCorpOptions)
{
    private readonly VoidCorpOptions _options = voidCorpOptions.Value;

    public VoidCorpCatalogDto GetCatalog()
    {
        EnsureSynced();
        var document = VoidCorpCatalogSync.Load(hostingPaths.VoidCorpCacheRoot);
        return new VoidCorpCatalogDto(
            document.UpdatedAtUtc,
            document.Products.Select(MapProduct).ToList());
    }

    public VoidCorpProductDto? TryGetProduct(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        EnsureSynced();
        var document = VoidCorpCatalogSync.Load(hostingPaths.VoidCorpCacheRoot);
        var entry = document.Products.FirstOrDefault(item =>
            item.Slug.Equals(slug.Trim(), StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : MapProduct(entry);
    }

    private void EnsureSynced()
    {
        if (!_options.Enabled)
        {
            return;
        }

        var syncResult = VoidCorpCatalogSync.Sync(
            hostingPaths.VoidCorpCacheRoot,
            tradeItemsCatalog.GetSupplyItems());
        backfillService.EnqueueAfterSync(syncResult);
    }

    private VoidCorpProductDto MapProduct(VoidCorpCatalogEntryDocument entry)
    {
        string? imageUrl = null;
        if (!string.IsNullOrWhiteSpace(entry.ImageFileName))
        {
            var imagePath = VoidCorpStoragePaths.ImageFilePath(hostingPaths.VoidCorpCacheRoot, entry.Slug);
            if (File.Exists(imagePath))
            {
                var cacheBust = File.GetLastWriteTimeUtc(imagePath).ToString("yyyyMMddHHmmss");
                imageUrl = VoidCorpStoragePaths.PublicProductUrl(entry.Slug, cacheBust);
            }
        }

        return new VoidCorpProductDto(
            entry.Slug,
            entry.DisplayName,
            entry.Category,
            entry.Tagline,
            entry.Summary,
            entry.Description,
            entry.BasePrice,
            entry.Color,
            entry.UiSymbol,
            imageUrl,
            entry.Source);
    }
}
