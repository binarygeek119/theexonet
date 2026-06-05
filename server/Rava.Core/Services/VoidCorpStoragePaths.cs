namespace Rava.Core.Services;

public static class VoidCorpStoragePaths
{
    public const string ImagesFolder = "images";
    public const string CatalogFileName = "catalog.json";
    public const string PublicProductsPath = "/exonet/voidcorp/products";

    public static string CatalogPath(string cacheRoot) =>
        Path.Combine(cacheRoot, CatalogFileName);

    public static string ImageFilePath(string cacheRoot, string slug) =>
        Path.Combine(cacheRoot, ImagesFolder, $"{slug}.jpg");

    public static string PublicProductUrl(string slug) =>
        $"{PublicProductsPath}/{Uri.EscapeDataString(slug)}.jpg";

    public static void EnsureDirectories(string cacheRoot) =>
        Directory.CreateDirectory(Path.Combine(cacheRoot, ImagesFolder));
}
