using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsImagePathsTests
{
    [Theory]
    [InlineData("/exonet/offworld-news/images/2026-06-01/story-one.jpg", true)]
    [InlineData("/exonet/offworld-news/images/2026/06/2026-06-01/story-one.jpg", true)]
    [InlineData("/exonet/offworld-news/placeholders/markets.svg", false)]
    [InlineData(null, false)]
    public void IsGeneratedImageUrl_DetectsAiImagePaths(string? url, bool expected)
    {
        Assert.Equal(expected, OffworldNewsImagePaths.IsGeneratedImageUrl(url));
    }

    [Fact]
    public void IsGeneratedImageUrlForEdition_RequiresMatchingEditionDate()
    {
        var editionDate = new DateOnly(2026, 6, 1);

        Assert.True(OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(
            "/exonet/offworld-news/images/2026-06-01/story-one.jpg",
            editionDate));
        Assert.True(OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(
            "/exonet/offworld-news/images/2026/06/2026-06-01/story-one.jpg",
            editionDate));
        Assert.False(OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(
            "/exonet/offworld-news/images/2026-05-30/story-one.jpg",
            editionDate));
        Assert.False(OffworldNewsImagePaths.IsGeneratedImageUrlForEdition(
            "/exonet/offworld-news/placeholders/markets.svg",
            editionDate));
    }

    [Fact]
    public void LostTransmissionImagePath_IsStablePlaceholder()
    {
        Assert.Equal(
            "/exonet/offworld-news/placeholders/lost-transmission.svg",
            OffworldNewsImagePaths.LostTransmissionImagePath);
        Assert.True(OffworldNewsImagePaths.IsLostTransmissionImageUrl(
            OffworldNewsImagePaths.LostTransmissionImagePath));
        Assert.False(OffworldNewsImagePaths.IsGeneratedImageUrl(
            OffworldNewsImagePaths.LostTransmissionImagePath));
    }

    [Fact]
    public void TryResolveCacheFilePath_MapsLegacyAndCanonicalUrls()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), $"onn-images-{Guid.NewGuid():N}");
        var date = new DateOnly(2026, 6, 1);

        try
        {
            var legacyDir = OffworldNewsStoragePaths.LegacyImageDirectoryPath(cacheRoot, date);
            Directory.CreateDirectory(legacyDir);
            var legacyFile = Path.Combine(legacyDir, "story-one.jpg");
            File.WriteAllText(legacyFile, "legacy");

            var legacyUrl = "/exonet/offworld-news/images/2026-06-01/story-one.jpg";
            Assert.Equal(legacyFile, OffworldNewsImagePaths.TryResolveCacheFilePath(cacheRoot, legacyUrl));

            var canonicalDir = OffworldNewsStoragePaths.ImageDirectoryPath(cacheRoot, date);
            Directory.CreateDirectory(canonicalDir);
            var canonicalFile = Path.Combine(canonicalDir, "story-two.jpg");
            File.WriteAllText(canonicalFile, "canonical");

            var canonicalUrl = OffworldNewsStoragePaths.BuildPublicImageUrl(date, "story-two.jpg");
            Assert.Equal(canonicalFile, OffworldNewsImagePaths.TryResolveCacheFilePath(cacheRoot, canonicalUrl));
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
