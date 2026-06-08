using Theexonet.Core.Constants;

namespace Theexonet.Core.Tests;

public class PlayerJobCatalogTests
{
    [Fact]
    public void TryGet_ResolvesAsteroidMiner()
    {
        var job = PlayerJobCatalog.TryGet(PlayerJobCatalog.AsteroidMiner);
        Assert.NotNull(job);
        Assert.Equal("Asteroid Miner", job!.Title);
    }

    [Fact]
    public void TryGet_RejectsUnknownSlug()
    {
        Assert.Null(PlayerJobCatalog.TryGet("lunar_barista"));
    }
}
