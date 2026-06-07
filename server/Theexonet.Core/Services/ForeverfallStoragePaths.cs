namespace Theexonet.Core.Services;

public static class ForeverfallStoragePaths
{
    public const string RostersFolder = "rosters";
    public const string ImagesFolder = "images";
    public const string ImageRegistryFileName = "image-registry.json";
    public const string PublicImagesPath = "/exonet/foreverfall/images";

    public static string RosterFilePath(string cacheRoot, DateOnly intakeDate) =>
        Path.Combine(cacheRoot, RostersFolder, $"{intakeDate:yyyy-MM-dd}.json");

    public static string? ResolveRosterFilePath(string cacheRoot, DateOnly intakeDate)
    {
        var path = RosterFilePath(cacheRoot, intakeDate);
        return File.Exists(path) ? path : null;
    }

    public static string ImageFilePath(string cacheRoot, string imageId) =>
        Path.Combine(cacheRoot, ImagesFolder, $"{imageId}.jpg");

    public static string ImageRegistryPath(string cacheRoot) =>
        Path.Combine(cacheRoot, ImageRegistryFileName);

    public static string PublicImageUrl(string imageId) =>
        $"{PublicImagesPath}/{Uri.EscapeDataString(imageId)}.jpg";

    public static bool ImageFileExists(string cacheRoot, string imageId) =>
        !string.IsNullOrWhiteSpace(imageId)
        && File.Exists(ImageFilePath(cacheRoot, imageId));

    public static string ResolvePublicImageUrl(string cacheRoot, string imageId) =>
        ImageFileExists(cacheRoot, imageId) ? PublicImageUrl(imageId) : string.Empty;

    public static void EnsureDirectories(string cacheRoot)
    {
        Directory.CreateDirectory(Path.Combine(cacheRoot, RostersFolder));
        Directory.CreateDirectory(Path.Combine(cacheRoot, ImagesFolder));
    }

    public static IEnumerable<string> EnumerateRosterFiles(string cacheRoot)
    {
        var dir = Path.Combine(cacheRoot, RostersFolder);
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            yield return path;
        }
    }

    public static int CountRosterFiles(string cacheRoot) =>
        Directory.Exists(Path.Combine(cacheRoot, RostersFolder))
            ? Directory.EnumerateFiles(Path.Combine(cacheRoot, RostersFolder), "*.json").Count()
            : 0;

    public static bool TryParseRosterDate(string filePath, out DateOnly date)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return DateOnly.TryParse(fileName, out date);
    }

    public static bool IsRosterExpired(DateOnly rosterDate, DateOnly today, int retentionDays) =>
        rosterDate < today.AddDays(-Math.Max(1, retentionDays));
}
