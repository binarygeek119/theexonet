namespace Rava.Core.Services;

/// <summary>
/// On-disk layout for Offworld News editions and AI illustrations.
/// Canonical layout: editions/yyyy/MM/yyyy-MM-dd.json and images/yyyy/MM/yyyy-MM-dd/*.jpg
/// Legacy flat layout remains readable until migrated.
/// </summary>
public static class OffworldNewsStoragePaths
{
    public const string EditionsFolder = "editions";
    public const string ImagesFolder = "images";
    public const string LayoutMigrationMarkerFile = ".storage-layout-v2.complete";

    public static string EditionRelativePath(DateOnly editionDate) =>
        Path.Combine(
            editionDate.Year.ToString("D4"),
            editionDate.Month.ToString("D2"),
            $"{editionDate:yyyy-MM-dd}.json");

    public static string EditionFilePath(string cacheRoot, DateOnly editionDate) =>
        Path.Combine(cacheRoot, EditionsFolder, EditionRelativePath(editionDate));

    public static string LegacyEditionFilePath(string cacheRoot, DateOnly editionDate) =>
        Path.Combine(cacheRoot, EditionsFolder, $"{editionDate:yyyy-MM-dd}.json");

    public static string? ResolveEditionFilePath(string cacheRoot, DateOnly editionDate)
    {
        var canonical = EditionFilePath(cacheRoot, editionDate);
        if (File.Exists(canonical))
        {
            return canonical;
        }

        var legacy = LegacyEditionFilePath(cacheRoot, editionDate);
        return File.Exists(legacy) ? legacy : null;
    }

    public static string ImageDayRelativePath(DateOnly editionDate) =>
        Path.Combine(
            editionDate.Year.ToString("D4"),
            editionDate.Month.ToString("D2"),
            editionDate.ToString("yyyy-MM-dd"));

    public static string ImageDirectoryPath(string cacheRoot, DateOnly editionDate) =>
        Path.Combine(cacheRoot, ImagesFolder, ImageDayRelativePath(editionDate));

    public static string LegacyImageDirectoryPath(string cacheRoot, DateOnly editionDate) =>
        Path.Combine(cacheRoot, ImagesFolder, editionDate.ToString("yyyy-MM-dd"));

    public static string BuildPublicImageUrl(DateOnly editionDate, string fileName) =>
        $"{OffworldNewsImagePaths.PublicImagesPrefix}{ToPublicRelativePath(ImageDayRelativePath(editionDate), fileName)}";

    public static string? UpgradeImageUrlToCanonical(string? imageUrl)
    {
        if (!OffworldNewsImagePaths.TryParseGeneratedImageUrl(imageUrl, out var editionDate, out var fileName))
        {
            return imageUrl;
        }

        return BuildPublicImageUrl(editionDate, fileName);
    }

    public static IEnumerable<string> EnumerateEditionFiles(string cacheRoot)
    {
        var editionsDir = Path.Combine(cacheRoot, EditionsFolder);
        if (!Directory.Exists(editionsDir))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(editionsDir, "*.json", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (DateOnly.TryParse(fileName, out _))
            {
                yield return path;
            }
        }
    }

    public static int CountEditionFiles(string cacheRoot) =>
        EnumerateEditionFiles(cacheRoot).Count();

    public static void EnsureEditionDirectories(string cacheRoot, DateOnly editionDate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(EditionFilePath(cacheRoot, editionDate))!);
        Directory.CreateDirectory(ImageDirectoryPath(cacheRoot, editionDate));
    }

    public static void DeleteEditionImageDirectories(string cacheRoot, DateOnly editionDate)
    {
        DeleteDirectoryIfExists(ImageDirectoryPath(cacheRoot, editionDate));
        DeleteDirectoryIfExists(LegacyImageDirectoryPath(cacheRoot, editionDate));
    }

    public static void DeleteLegacyEditionFile(string cacheRoot, DateOnly editionDate)
    {
        var legacy = LegacyEditionFilePath(cacheRoot, editionDate);
        if (!File.Exists(legacy))
        {
            return;
        }

        File.Delete(legacy);
    }

    private static string ToPublicRelativePath(string dayRelativePath, string fileName) =>
        $"{dayRelativePath.Replace('\\', '/').Trim('/')}/{fileName}";

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }
}
