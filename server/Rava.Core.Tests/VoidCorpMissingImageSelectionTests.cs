using Rava.Core.Services;

namespace Rava.Core.Tests;

public class VoidCorpMissingImageSelectionTests
{
    [Fact]
    public void SelectMissing_returns_products_without_image_file_name()
    {
        var root = CreateTempDirectory();
        try
        {
            var products = new List<VoidCorpCatalogEntryDocument>
            {
                CreateProduct("DrillBits", imageFileName: null),
                CreateProduct("FuelCells", imageFileName: "FuelCells.jpg"),
            };

            WriteImage(root, "FuelCells");

            var missing = VoidCorpMissingImageSelection.SelectMissing(root, products, maxCount: 4);

            Assert.Single(missing);
            Assert.Equal("DrillBits", missing[0].Slug);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelectMissing_returns_products_when_referenced_file_is_missing()
    {
        var root = CreateTempDirectory();
        try
        {
            var products = new List<VoidCorpCatalogEntryDocument>
            {
                CreateProduct("DrillBits", imageFileName: "DrillBits.jpg"),
            };

            var missing = VoidCorpMissingImageSelection.SelectMissing(root, products, maxCount: 4);

            Assert.Single(missing);
            Assert.Equal("DrillBits", missing[0].Slug);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelectMissing_caps_results_at_max_count()
    {
        var root = CreateTempDirectory();
        try
        {
            var products = new List<VoidCorpCatalogEntryDocument>
            {
                CreateProduct("DrillBits", imageFileName: null),
                CreateProduct("FuelCells", imageFileName: null),
                CreateProduct("LifeSupport", imageFileName: null),
            };

            var missing = VoidCorpMissingImageSelection.SelectMissing(root, products, maxCount: 2);

            Assert.Equal(2, missing.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelectMissing_ignores_products_with_existing_image_files()
    {
        var root = CreateTempDirectory();
        try
        {
            var products = new List<VoidCorpCatalogEntryDocument>
            {
                CreateProduct("DrillBits", imageFileName: "DrillBits.jpg"),
                CreateProduct("FuelCells", imageFileName: "FuelCells.jpg"),
            };

            WriteImage(root, "DrillBits");
            WriteImage(root, "FuelCells");

            var missing = VoidCorpMissingImageSelection.SelectMissing(root, products, maxCount: 4);

            Assert.Empty(missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static VoidCorpCatalogEntryDocument CreateProduct(string slug, string? imageFileName) =>
        new(
            slug,
            slug,
            "Industrial Supply",
            "Tagline",
            "Summary",
            "Description",
            100m,
            "#888888",
            "XLI",
            imageFileName,
            "template");

    private static void WriteImage(string root, string slug)
    {
        VoidCorpStoragePaths.EnsureDirectories(root);
        File.WriteAllBytes(VoidCorpStoragePaths.ImageFilePath(root, slug), [0xFF, 0xD8, 0xFF]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"voidcorp-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
