using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class VoidCorpImageBackfillPolicyTests
{
    [Fact]
    public void ShouldEnqueueAfterSync_returns_true_when_items_added()
    {
        var syncResult = new VoidCorpCatalogSyncResult(Added: 1, Updated: 0, Unchanged: 3, MissingImages: 1);

        Assert.True(VoidCorpImageBackfillPolicy.ShouldEnqueueAfterSync(syncResult));
    }

    [Fact]
    public void ShouldEnqueueAfterSync_returns_true_when_images_missing()
    {
        var syncResult = new VoidCorpCatalogSyncResult(Added: 0, Updated: 0, Unchanged: 4, MissingImages: 2);

        Assert.True(VoidCorpImageBackfillPolicy.ShouldEnqueueAfterSync(syncResult));
    }

    [Fact]
    public void ShouldEnqueueAfterSync_returns_false_when_catalog_is_fully_synced()
    {
        var syncResult = new VoidCorpCatalogSyncResult(Added: 0, Updated: 0, Unchanged: 4, MissingImages: 0);

        Assert.False(VoidCorpImageBackfillPolicy.ShouldEnqueueAfterSync(syncResult));
    }
}
