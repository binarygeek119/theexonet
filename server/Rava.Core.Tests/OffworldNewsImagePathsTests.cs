using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsImagePathsTests
{
    [Theory]
    [InlineData("/exonet/offworld-news/images/2026-06-01/story-one.jpg", true)]
    [InlineData("/exonet/offworld-news/placeholders/markets.svg", false)]
    [InlineData(null, false)]
    public void IsGeneratedImageUrl_DetectsAiImagePaths(string? url, bool expected)
    {
        Assert.Equal(expected, OffworldNewsImagePaths.IsGeneratedImageUrl(url));
    }

    [Fact]
    public void TryResolveCacheFilePath_MapsPublicUrlToCacheFile()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "offworld-news-test");
        var path = OffworldNewsImagePaths.TryResolveCacheFilePath(
            cacheRoot,
            "/exonet/offworld-news/images/2026-06-01/story-one.jpg");

        Assert.Equal(
            Path.Combine(cacheRoot, "images", "2026-06-01", "story-one.jpg"),
            path);
    }
}
