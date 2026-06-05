using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed partial class ForeverfallPortraitRegistryScanner : IExonetAiAssetScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    [GeneratedRegex(@"^FF-\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidImageIdRegex();

    public string AreaName => "Foreverfall";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        if (!context.ForeverfallEnabled)
        {
            return ExonetAiAssetScanAreaResult.SkippedArea(AreaName);
        }

        return Sync(context.HostingPaths.ForeverfallCacheRoot);
    }

    public static ExonetAiAssetScanAreaResult Sync(string cacheRoot)
    {
        var imagesDir = Path.Combine(cacheRoot, ForeverfallStoragePaths.ImagesFolder);
        Directory.CreateDirectory(imagesDir);

        var registry = LoadRegistry(cacheRoot);
        var knownIds = registry.Images
            .Select(entry => entry.ImageId)
            .ToHashSet(StringComparer.Ordinal);

        var imported = 0;
        var alreadyRegistered = 0;
        var skippedInvalid = 0;
        var entries = registry.Images.ToList();
        var nextNumber = Math.Max(1, registry.NextImageNumber);

        foreach (var imagePath in Directory.EnumerateFiles(imagesDir, "*.jpg", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(imagePath);
            var imageId = Path.GetFileNameWithoutExtension(fileName);
            if (!ValidImageIdRegex().IsMatch(imageId))
            {
                skippedInvalid++;
                continue;
            }

            if (knownIds.Contains(imageId))
            {
                alreadyRegistered++;
                continue;
            }

            var createdAt = File.Exists(imagePath)
                ? File.GetLastWriteTimeUtc(imagePath)
                : DateTime.UtcNow;

            entries.Add(new ForeverfallImageRegistryEntry(
                imageId,
                GenderHint: null,
                createdAt,
                fileName));
            knownIds.Add(imageId);
            imported++;

            if (int.TryParse(imageId.AsSpan(3), out var parsed))
            {
                nextNumber = Math.Max(nextNumber, parsed + 1);
            }
        }

        if (imported > 0)
        {
            SaveRegistry(cacheRoot, new ForeverfallImageRegistry(entries, nextNumber));
        }

        return new ExonetAiAssetScanAreaResult(
            "Foreverfall",
            Imported: imported,
            AlreadyRegistered: alreadyRegistered,
            SkippedInvalid: skippedInvalid);
    }

    private static ForeverfallImageRegistry LoadRegistry(string cacheRoot)
    {
        var path = ForeverfallStoragePaths.ImageRegistryPath(cacheRoot);
        if (!File.Exists(path))
        {
            return new ForeverfallImageRegistry([], 1);
        }

        try
        {
            return JsonSerializer.Deserialize<ForeverfallImageRegistry>(File.ReadAllText(path), JsonOptions)
                ?? new ForeverfallImageRegistry([], 1);
        }
        catch (JsonException)
        {
            return new ForeverfallImageRegistry([], 1);
        }
    }

    private static void SaveRegistry(string cacheRoot, ForeverfallImageRegistry registry)
    {
        ForeverfallStoragePaths.EnsureDirectories(cacheRoot);
        var path = ForeverfallStoragePaths.ImageRegistryPath(cacheRoot);
        File.WriteAllText(path, JsonSerializer.Serialize(registry, JsonOptions));
    }
}
