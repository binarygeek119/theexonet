namespace Rava.Core.Services;

public static class VoidCorpMissingImageSelection
{
    public static IReadOnlyList<VoidCorpCatalogEntryDocument> SelectMissing(
        string cacheRoot,
        IReadOnlyList<VoidCorpCatalogEntryDocument> products,
        int maxCount)
    {
        var limit = Math.Max(1, maxCount);
        return products
            .Where(product => IsMissing(cacheRoot, product))
            .Take(limit)
            .ToList();
    }

    public static bool IsMissing(string cacheRoot, VoidCorpCatalogEntryDocument product) =>
        string.IsNullOrWhiteSpace(product.ImageFileName)
        || !File.Exists(VoidCorpStoragePaths.ImageFilePath(cacheRoot, product.Slug));
}
