namespace Rava.Core.Configuration;

/// <summary>
/// Production config and CSV spreadsheets live outside the publish folder (default: /var/www/data).
/// Set environment variable RAVA_DATA_DIR on systemd services.
/// </summary>
public static class RavaDataPaths
{
    public const string EnvironmentVariable = "RAVA_DATA_DIR";

    public const string DefaultProductionPath = "/var/www/data";

    public const string DefaultPublishPath = "/var/www/publish";

    public static string Resolve(string contentRootPath)
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv.Trim());
        }

        var normalizedContent = Path.GetFullPath(contentRootPath);
        var productionDataAppsettings = Path.Combine(DefaultProductionPath, "appsettings.json");
        if (File.Exists(productionDataAppsettings)
            && IsUnderOrEqual(normalizedContent, DefaultPublishPath))
        {
            return DefaultProductionPath;
        }

        return normalizedContent;
    }

    private static bool IsUnderOrEqual(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveFile(string contentRootPath, string fileName) =>
        Path.Combine(Resolve(contentRootPath), fileName);

    /// <summary>
    /// Player-uploaded images (avatars, backgrounds). Uses /var/www/data/images in production.
    /// </summary>
    public static string ResolveImagesRoot(string contentRootPath, string webRootPath)
    {
        var dataRoot = Resolve(contentRootPath);
        if (!string.Equals(dataRoot, contentRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(dataRoot, "images");
        }

        return Path.Combine(webRootPath, "images");
    }

    public const string OffworldNewsPublicPath = "exonet/offworld-news";

    /// <summary>
    /// Writable Exonet content (Offworld News editions and images). Uses /var/www/data/exonet in production.
    /// </summary>
    public static string ResolveExonetRoot(string contentRootPath, string webRootPath)
    {
        var dataRoot = Resolve(contentRootPath);
        if (!string.Equals(dataRoot, contentRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(dataRoot, "exonet");
        }

        return Path.Combine(webRootPath, "exonet");
    }

    public static string ResolveOffworldNewsCacheRoot(
        string contentRootPath,
        string webRootPath,
        string configuredCacheDirectory)
    {
        var segment = configuredCacheDirectory.Trim().TrimStart('/').Replace('\\', '/');
        if (segment.StartsWith("exonet/", StringComparison.OrdinalIgnoreCase))
        {
            segment = segment["exonet/".Length..];
        }

        return Path.Combine(ResolveExonetRoot(contentRootPath, webRootPath), segment);
    }
}
