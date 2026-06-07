using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class TestingDummyFriendsCatalogTests
{
    [Fact]
    public void TryGet_returns_profile_for_valid_index()
    {
        var profile = TestingDummyFriendsCatalog.TryGet(0);

        Assert.NotNull(profile);
        Assert.Equal("vein_runner", profile!.Username);
    }

    [Fact]
    public void TryGet_returns_null_for_invalid_index()
    {
        Assert.Null(TestingDummyFriendsCatalog.TryGet(-1));
        Assert.Null(TestingDummyFriendsCatalog.TryGet(TestingDummyFriendsCatalog.DummyCount));
    }
}
