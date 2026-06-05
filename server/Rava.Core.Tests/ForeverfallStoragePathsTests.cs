using Rava.Core.Services;

namespace Rava.Core.Tests;

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
}
