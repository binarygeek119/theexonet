using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ForeverfallStoragePathsTests
{
    [Fact]
    public void IsRosterExpired_UsesRetentionDays()
    {
        var today = new DateOnly(2026, 6, 15);
        var expired = new DateOnly(2026, 5, 30);
        var retained = new DateOnly(2026, 6, 2);

        Assert.True(ForeverfallStoragePaths.IsRosterExpired(expired, today, retentionDays: 14));
        Assert.False(ForeverfallStoragePaths.IsRosterExpired(retained, today, retentionDays: 14));
    }

    [Fact]
    public void PublicImageUrl_EscapesImageId()
    {
        var url = ForeverfallStoragePaths.PublicImageUrl("FF-0001");
        Assert.Equal("/exonet/foreverfall/images/FF-0001.jpg", url);
    }

    [Fact]
    public void ResolvePublicImageUrl_returns_empty_when_file_missing()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "ffp-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ForeverfallStoragePaths.EnsureDirectories(cacheRoot);

            var url = ForeverfallStoragePaths.ResolvePublicImageUrl(cacheRoot, "FF-0001");

            Assert.Equal(string.Empty, url);
        }
        finally
        {
            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePublicImageUrl_returns_public_url_when_file_exists()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "ffp-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ForeverfallStoragePaths.EnsureDirectories(cacheRoot);
            File.WriteAllText(ForeverfallStoragePaths.ImageFilePath(cacheRoot, "FF-0042"), "fake");

            var url = ForeverfallStoragePaths.ResolvePublicImageUrl(cacheRoot, "FF-0042");

            Assert.Equal("/exonet/foreverfall/images/FF-0042.jpg", url);
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
