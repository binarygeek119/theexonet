using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsStoragePathsTests
{
    [Fact]
    public void EditionFilePath_UsesYearMonthFolders()
    {
        var date = new DateOnly(2026, 5, 29);
        var path = OffworldNewsStoragePaths.EditionFilePath("/cache", date);

        Assert.EndsWith(
            Path.Combine("editions", "2026", "05", "2026-05-29.json"),
            path);
    }

    [Fact]
    public void BuildPublicImageUrl_UsesYearMonthDayFolders()
    {
        var date = new DateOnly(2026, 5, 29);
        var url = OffworldNewsStoragePaths.BuildPublicImageUrl(date, "belt-strike-1.jpg");

        Assert.Equal(
            "/exonet/offworld-news/images/2026/05/2026-05-29/belt-strike-1.jpg",
            url);
    }

    [Fact]
    public void UpgradeImageUrlToCanonical_RewritesLegacyUrls()
    {
        var legacy = "/exonet/offworld-news/images/2026-05-29/belt-strike-1.jpg";
        var upgraded = OffworldNewsStoragePaths.UpgradeImageUrlToCanonical(legacy);

        Assert.Equal(
            "/exonet/offworld-news/images/2026/05/2026-05-29/belt-strike-1.jpg",
            upgraded);
    }

    [Fact]
    public void ResolveEditionFilePath_FindsLegacyThenCanonical()
    {
        var root = Path.Combine(Path.GetTempPath(), $"onn-storage-{Guid.NewGuid():N}");
        var date = new DateOnly(2026, 3, 15);

        try
        {
            var legacy = OffworldNewsStoragePaths.LegacyEditionFilePath(root, date);
            Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
            File.WriteAllText(legacy, "{}");

            Assert.Equal(legacy, OffworldNewsStoragePaths.ResolveEditionFilePath(root, date));

            var canonical = OffworldNewsStoragePaths.EditionFilePath(root, date);
            Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
            File.WriteAllText(canonical, "{\"editionDate\":\"2026-03-15\"}");

            Assert.Equal(canonical, OffworldNewsStoragePaths.ResolveEditionFilePath(root, date));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
