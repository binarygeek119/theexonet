using System.Text.Json;
using System.Text.Json.Serialization;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;
using Theexonet.Core.Services.ExonetAiAssetScan;

namespace Theexonet.Core.Tests;

public class OffworldNewsImageScannerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    [Fact]
    public void Scan_reports_no_issues_when_edition_references_existing_image()
    {
        var root = CreateTempDirectory();
        try
        {
            var editionDate = new DateOnly(2026, 6, 1);
            var fileName = "story-1.jpg";
            var imageUrl = OffworldNewsStoragePaths.BuildPublicImageUrl(editionDate, fileName);
            WriteEdition(root, editionDate, imageUrl);

            var imageDir = OffworldNewsStoragePaths.ImageDirectoryPath(root, editionDate);
            Directory.CreateDirectory(imageDir);
            File.WriteAllText(Path.Combine(imageDir, fileName), "image");

            var result = Scan(root);

            Assert.Equal(0, result.Orphans);
            Assert.Equal(0, result.Missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_reports_orphan_image_not_referenced_by_editions()
    {
        var root = CreateTempDirectory();
        try
        {
            var editionDate = new DateOnly(2026, 6, 2);
            WriteEdition(root, editionDate, imageUrl: null);

            var imageDir = OffworldNewsStoragePaths.ImageDirectoryPath(root, editionDate);
            Directory.CreateDirectory(imageDir);
            File.WriteAllText(Path.Combine(imageDir, "orphan.jpg"), "orphan");

            var result = Scan(root);

            Assert.Equal(1, result.Orphans);
            Assert.Equal(0, result.Missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_reports_missing_image_referenced_by_edition()
    {
        var root = CreateTempDirectory();
        try
        {
            var editionDate = new DateOnly(2026, 6, 3);
            var imageUrl = OffworldNewsStoragePaths.BuildPublicImageUrl(editionDate, "missing.jpg");
            WriteEdition(root, editionDate, imageUrl);

            var result = Scan(root);

            Assert.Equal(0, result.Orphans);
            Assert.Equal(1, result.Missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ExonetAiAssetScanAreaResult Scan(string cacheRoot)
    {
        var hostingPaths = new TheexonetHostingPaths
        {
            DataRoot = cacheRoot,
            ImagesRoot = cacheRoot,
            OffworldNewsCacheRoot = cacheRoot,
            LunarWeatherCacheRoot = cacheRoot,
            ForeverfallCacheRoot = cacheRoot,
            VoidCorpCacheRoot = cacheRoot,
            WebRoot = cacheRoot,
        };

        var context = new ExonetAiAssetScanContext(
            hostingPaths,
            OffworldNewsReportersCsvPath: string.Empty,
            ForeverfallEnabled: false,
            OffworldNewsEnabled: true,
            LunarWeatherEnabled: false,
            VoidCorpEnabled: false,
            SupplyItems: []);

        return new OffworldNewsImageScanner().Scan(context);
    }

    private static void WriteEdition(string cacheRoot, DateOnly editionDate, string? imageUrl)
    {
        OffworldNewsStoragePaths.EnsureEditionDirectories(cacheRoot, editionDate);
        var edition = new OffworldNewsEditionDto(
            editionDate,
            DateTime.UtcNow,
            "template",
            [
                new OffworldNewsStoryDto(
                    "story-1",
                    "Headline",
                    "Dek",
                    "Body",
                    "Markets",
                    "Ceres",
                    "Author",
                    "author-slug",
                    DateTime.UtcNow,
                    CompanyName: null,
                    ImageUrl: imageUrl),
            ]);

        File.WriteAllText(
            OffworldNewsStoragePaths.EditionFilePath(cacheRoot, editionDate),
            JsonSerializer.Serialize(edition, JsonOptions));
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"onn-img-scan-{Guid.NewGuid():N}");
}
