using System.Text.Json;
using System.Text.Json.Serialization;
using Theexonet.Core.Services;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Tests;

public class OffworldNewsStorageLayoutMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    [Fact]
    public void RunIfNeeded_MovesLegacyEditionAndImagesIntoYearMonthFolders()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), $"onn-migrate-{Guid.NewGuid():N}");
        var date = new DateOnly(2026, 4, 10);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        try
        {
            var legacyEditionPath = OffworldNewsStoragePaths.LegacyEditionFilePath(cacheRoot, date);
            Directory.CreateDirectory(Path.GetDirectoryName(legacyEditionPath)!);

            var legacyImageDir = OffworldNewsStoragePaths.LegacyImageDirectoryPath(cacheRoot, date);
            Directory.CreateDirectory(legacyImageDir);
            File.WriteAllText(Path.Combine(legacyImageDir, "strike-one.jpg"), "image");

            var edition = new OffworldNewsEditionDto(
                date,
                DateTime.UtcNow,
                "openai",
                [
                    new OffworldNewsStoryDto(
                        "strike-one",
                        "Headline",
                        "Dek",
                        "Body",
                        "Markets",
                        "Ceres Relay",
                        "Reporter",
                        "reporter",
                        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        "VoidCorp",
                        $"/exonet/offworld-news/images/{date:yyyy-MM-dd}/strike-one.jpg",
                        "landscape"),
                ]);

            File.WriteAllText(legacyEditionPath, JsonSerializer.Serialize(edition, JsonOptions));

            OffworldNewsStorageLayoutMigration.RunIfNeeded(cacheRoot, logger);

            Assert.False(File.Exists(legacyEditionPath));
            Assert.True(File.Exists(OffworldNewsStoragePaths.EditionFilePath(cacheRoot, date)));
            Assert.True(File.Exists(Path.Combine(
                OffworldNewsStoragePaths.ImageDirectoryPath(cacheRoot, date),
                "strike-one.jpg")));
            Assert.True(File.Exists(Path.Combine(cacheRoot, OffworldNewsStoragePaths.LayoutMigrationMarkerFile)));

            var migrated = JsonSerializer.Deserialize<OffworldNewsEditionDto>(
                File.ReadAllText(OffworldNewsStoragePaths.EditionFilePath(cacheRoot, date)),
                JsonOptions);
            Assert.Equal(
                OffworldNewsStoragePaths.BuildPublicImageUrl(date, "strike-one.jpg"),
                migrated!.Stories[0].ImageUrl);
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }
}
