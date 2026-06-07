namespace Theexonet.Core.Services;

public static class LunarWeatherStoragePaths
{
    public const string EditionsFolder = "editions";

    public static string EditionFilePath(string cacheRoot, DateOnly bulletinDate) =>
        Path.Combine(cacheRoot, EditionsFolder, $"{bulletinDate:yyyy-MM-dd}.json");

    public static string? ResolveEditionFilePath(string cacheRoot, DateOnly bulletinDate)
    {
        var path = EditionFilePath(cacheRoot, bulletinDate);
        return File.Exists(path) ? path : null;
    }

    public static void EnsureEditionDirectory(string cacheRoot)
    {
        Directory.CreateDirectory(Path.Combine(cacheRoot, EditionsFolder));
    }

    public static IEnumerable<string> EnumerateEditionFiles(string cacheRoot)
    {
        var dir = Path.Combine(cacheRoot, EditionsFolder);
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            yield return path;
        }
    }

    public static int CountEditionFiles(string cacheRoot) =>
        Directory.Exists(Path.Combine(cacheRoot, EditionsFolder))
            ? Directory.EnumerateFiles(Path.Combine(cacheRoot, EditionsFolder), "*.json").Count()
            : 0;
}
