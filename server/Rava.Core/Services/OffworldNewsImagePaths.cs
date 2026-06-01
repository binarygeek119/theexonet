namespace Rava.Core.Services;

/// <summary>
/// Public URL and cache path helpers for Offworld News story illustrations.
/// </summary>
public static class OffworldNewsImagePaths
{
    public const string PublicImagesPrefix = "/exonet/offworld-news/images/";

    public static bool IsGeneratedImageUrl(string? imageUrl) =>
        !string.IsNullOrWhiteSpace(imageUrl)
        && imageUrl.StartsWith(PublicImagesPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a public /exonet/offworld-news/images/... URL to a file under the cache root.
    /// </summary>
    public static string? TryResolveCacheFilePath(string cacheRoot, string? imageUrl)
    {
        if (!IsGeneratedImageUrl(imageUrl))
        {
            return null;
        }

        var relative = imageUrl![PublicImagesPrefix.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        return Path.Combine(cacheRoot, "images", relative);
    }

    public static bool GeneratedImageExists(string cacheRoot, string? imageUrl)
    {
        var path = TryResolveCacheFilePath(cacheRoot, imageUrl);
        return path is not null && File.Exists(path);
    }
}
