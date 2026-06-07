namespace Theexonet.Core.Services;

/// <summary>
/// Public URL and cache path helpers for Offworld News story illustrations.
/// </summary>
public static class OffworldNewsImagePaths
{
    public const string PublicImagesPrefix = "/exonet/offworld-news/images/";

    public const string LostTransmissionImagePath = "/exonet/offworld-news/placeholders/lost-transmission.svg";

    public static bool IsLostTransmissionImageUrl(string? imageUrl) =>
        !string.IsNullOrWhiteSpace(imageUrl)
        && imageUrl.Equals(LostTransmissionImagePath, StringComparison.OrdinalIgnoreCase);

    public static bool IsGeneratedImageUrl(string? imageUrl) =>
        !string.IsNullOrWhiteSpace(imageUrl)
        && imageUrl.StartsWith(PublicImagesPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseGeneratedImageUrl(string? imageUrl, out DateOnly editionDate, out string fileName)
    {
        editionDate = default;
        fileName = string.Empty;

        if (!IsGeneratedImageUrl(imageUrl))
        {
            return false;
        }

        var relative = imageUrl![PublicImagesPrefix.Length..].TrimStart('/').Replace('\\', '/');
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && DateOnly.TryParse(parts[0], out editionDate))
        {
            fileName = parts[1];
            return !string.IsNullOrWhiteSpace(fileName);
        }

        if (parts.Length == 4
            && int.TryParse(parts[0], out var year)
            && int.TryParse(parts[1], out var month)
            && DateOnly.TryParse(parts[2], out editionDate)
            && editionDate.Year == year
            && editionDate.Month == month)
        {
            fileName = parts[3];
            return !string.IsNullOrWhiteSpace(fileName);
        }

        return false;
    }

    public static bool IsGeneratedImageUrlForEdition(string? imageUrl, DateOnly editionDate)
    {
        if (!TryParseGeneratedImageUrl(imageUrl, out var imageEditionDate, out _))
        {
            return false;
        }

        return imageEditionDate == editionDate;
    }

    /// <summary>
    /// Maps a public /exonet/offworld-news/images/... URL to a file under the cache root.
    /// </summary>
    public static string? TryResolveCacheFilePath(string cacheRoot, string? imageUrl)
    {
        if (!TryParseGeneratedImageUrl(imageUrl, out var editionDate, out var fileName))
        {
            return null;
        }

        var canonical = Path.Combine(
            cacheRoot,
            OffworldNewsStoragePaths.ImagesFolder,
            OffworldNewsStoragePaths.ImageDayRelativePath(editionDate),
            fileName);
        if (File.Exists(canonical))
        {
            return canonical;
        }

        var legacy = Path.Combine(
            cacheRoot,
            OffworldNewsStoragePaths.ImagesFolder,
            editionDate.ToString("yyyy-MM-dd"),
            fileName);
        return legacy;
    }

    public static bool GeneratedImageExists(string cacheRoot, string? imageUrl)
    {
        var path = TryResolveCacheFilePath(cacheRoot, imageUrl);
        return path is not null && File.Exists(path);
    }
}
