using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;
using Rava.Core.Services.ExonetAiAssetScan;

namespace Rava.Core.Tests;

public class LunarWeatherEditionScannerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Scan_accepts_valid_edition_json()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteValidBulletin(root, new DateOnly(2026, 6, 1));

            var result = Scan(root);

            Assert.Equal(0, result.Invalid);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_counts_invalid_edition_files()
    {
        var root = CreateTempDirectory();
        try
        {
            LunarWeatherStoragePaths.EnsureEditionDirectory(root);
            File.WriteAllText(
                Path.Combine(root, LunarWeatherStoragePaths.EditionsFolder, "not-a-date.json"),
                "{}");
            File.WriteAllText(
                Path.Combine(root, LunarWeatherStoragePaths.EditionsFolder, "2026-06-02.json"),
                "{ invalid json");

            var result = Scan(root);

            Assert.Equal(2, result.Invalid);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ExonetAiAssetScanAreaResult Scan(string cacheRoot)
    {
        var hostingPaths = new RavaHostingPaths
        {
            DataRoot = cacheRoot,
            ImagesRoot = cacheRoot,
            OffworldNewsCacheRoot = cacheRoot,
            LunarWeatherCacheRoot = cacheRoot,
            ForeverfallCacheRoot = cacheRoot,
            WebRoot = cacheRoot,
        };

        var context = new ExonetAiAssetScanContext(
            hostingPaths,
            OffworldNewsReportersCsvPath: string.Empty,
            ForeverfallEnabled: false,
            OffworldNewsEnabled: false,
            LunarWeatherEnabled: true);

        return new LunarWeatherEditionScanner().Scan(context);
    }

    private static void WriteValidBulletin(string cacheRoot, DateOnly bulletinDate)
    {
        LunarWeatherStoragePaths.EnsureEditionDirectory(cacheRoot);
        var bulletin = new LunarWeatherBulletinDto(
            bulletinDate,
            DateTime.UtcNow,
            "template",
            RelayPoolSize: 100,
            TargetOperationalCount: 90,
            OperationalCount: 88,
            OutageCount: 2,
            Readings: [],
            Outages: []);

        File.WriteAllText(
            LunarWeatherStoragePaths.EditionFilePath(cacheRoot, bulletinDate),
            JsonSerializer.Serialize(bulletin, JsonOptions));
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"lw-scan-{Guid.NewGuid():N}");
}
