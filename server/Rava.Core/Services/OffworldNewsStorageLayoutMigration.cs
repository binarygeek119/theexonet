using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Rava.Core.Dtos;

namespace Rava.Core.Services;

/// <summary>
/// Moves flat editions/images folders into year/month hierarchy for long-term storage.
/// </summary>
public static class OffworldNewsStorageLayoutMigration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static void RunIfNeeded(string cacheRoot, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(cacheRoot))
        {
            return;
        }

        Directory.CreateDirectory(cacheRoot);
        var markerPath = Path.Combine(cacheRoot, OffworldNewsStoragePaths.LayoutMigrationMarkerFile);
        if (File.Exists(markerPath))
        {
            return;
        }

        var migratedEditions = 0;
        var migratedImages = 0;

        try
        {
            var legacyEditionDir = Path.Combine(cacheRoot, OffworldNewsStoragePaths.EditionsFolder);
            if (Directory.Exists(legacyEditionDir))
            {
                foreach (var legacyPath in Directory.EnumerateFiles(legacyEditionDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileNameWithoutExtension(legacyPath);
                    if (!DateOnly.TryParse(fileName, out var editionDate))
                    {
                        continue;
                    }

                    if (MigrateEdition(cacheRoot, legacyPath, editionDate, ref migratedImages))
                    {
                        migratedEditions++;
                    }
                }
            }

            MigrateOrphanLegacyImageFolders(cacheRoot, ref migratedImages);
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            logger.LogInformation(
                "Offworld News storage layout migration complete: {Editions} editions, {Images} images reorganized under {Root}",
                migratedEditions,
                migratedImages,
                cacheRoot);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Offworld News storage layout migration failed under {Root}", cacheRoot);
        }
    }

    private static bool MigrateEdition(
        string cacheRoot,
        string legacyEditionPath,
        DateOnly editionDate,
        ref int migratedImages)
    {
        OffworldNewsEditionDto? edition;
        try
        {
            edition = JsonSerializer.Deserialize<OffworldNewsEditionDto>(File.ReadAllText(legacyEditionPath), JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (edition is null)
        {
            return false;
        }

        var imageMoves = 0;
        var stories = edition.Stories
            .Select(story =>
            {
                var upgradedUrl = OffworldNewsStoragePaths.UpgradeImageUrlToCanonical(story.ImageUrl);
                if (string.Equals(upgradedUrl, story.ImageUrl, StringComparison.Ordinal))
                {
                    return story;
                }

                if (OffworldNewsImagePaths.TryParseGeneratedImageUrl(story.ImageUrl, out var imageDate, out var fileName)
                    && MigrateImageFile(cacheRoot, imageDate, fileName))
                {
                    imageMoves++;
                }

                return story with { ImageUrl = upgradedUrl };
            })
            .ToList();
        migratedImages += imageMoves;

        edition = edition with { Stories = stories };
        OffworldNewsStoragePaths.EnsureEditionDirectories(cacheRoot, editionDate);
        var targetPath = OffworldNewsStoragePaths.EditionFilePath(cacheRoot, editionDate);
        File.WriteAllText(targetPath, JsonSerializer.Serialize(edition, JsonOptions));

        if (!string.Equals(legacyEditionPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(legacyEditionPath);
        }

        return true;
    }

    private static void MigrateOrphanLegacyImageFolders(string cacheRoot, ref int migratedImages)
    {
        var imagesRoot = Path.Combine(cacheRoot, OffworldNewsStoragePaths.ImagesFolder);
        if (!Directory.Exists(imagesRoot))
        {
            return;
        }

        foreach (var legacyDayDir in Directory.EnumerateDirectories(imagesRoot))
        {
            var folderName = Path.GetFileName(legacyDayDir);
            if (!DateOnly.TryParse(folderName, out var editionDate))
            {
                continue;
            }

            foreach (var imagePath in Directory.EnumerateFiles(legacyDayDir))
            {
                migratedImages += MigrateImageFile(cacheRoot, editionDate, Path.GetFileName(imagePath)) ? 1 : 0;
            }

            if (!Directory.EnumerateFileSystemEntries(legacyDayDir).Any())
            {
                Directory.Delete(legacyDayDir, recursive: false);
            }
        }
    }

    private static bool MigrateImageFile(string cacheRoot, DateOnly editionDate, string fileName)
    {
        var targetDir = OffworldNewsStoragePaths.ImageDirectoryPath(cacheRoot, editionDate);
        Directory.CreateDirectory(targetDir);
        var targetPath = Path.Combine(targetDir, fileName);
        if (File.Exists(targetPath))
        {
            return false;
        }

        var legacyPath = Path.Combine(OffworldNewsStoragePaths.LegacyImageDirectoryPath(cacheRoot, editionDate), fileName);
        if (!File.Exists(legacyPath))
        {
            return false;
        }

        File.Move(legacyPath, targetPath);
        return true;
    }
}
