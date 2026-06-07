using Microsoft.Extensions.Options;
using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Core.Configuration;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.VoidCorp;

public sealed record VoidCorpMissingImageBackfillResult(int Attempted, int Generated, bool Skipped);

public sealed class VoidCorpMissingImageBackfillService(
    AiImageQueuePublisher aiImageQueuePublisher,
    TheexonetHostingPaths hostingPaths,
    ITradeItemsCatalog tradeItemsCatalog,
    IOptions<VoidCorpOptions> options,
    ILogger<VoidCorpMissingImageBackfillService> logger)
{
    public bool IsConfigured => options.Value.Enabled;

    public void EnqueueAfterSync(VoidCorpCatalogSyncResult syncResult)
    {
        if (!IsConfigured || !VoidCorpImageBackfillPolicy.ShouldEnqueueAfterSync(syncResult))
        {
            return;
        }

        _ = EnqueueMissingAsync("sync", CancellationToken.None);
    }

    public Task<VoidCorpMissingImageBackfillResult> RunAsync(
        string trigger,
        CancellationToken cancellationToken,
        bool waitForLock = true) =>
        EnqueueMissingAsync(trigger, cancellationToken);

    public async Task<VoidCorpMissingImageBackfillResult> RegenerateExistingAsync(
        string trigger,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("VoidCorp regenerate ({Trigger}) skipped: not configured.", trigger);
            return new VoidCorpMissingImageBackfillResult(0, 0, Skipped: true);
        }

        VoidCorpCatalogSync.Sync(hostingPaths.VoidCorpCacheRoot, tradeItemsCatalog.GetSupplyItems());
        var cacheRoot = hostingPaths.VoidCorpCacheRoot;
        var document = VoidCorpCatalogSync.Load(cacheRoot);
        var withImages = VoidCorpMissingImageSelection.SelectWithImages(cacheRoot, document.Products);
        if (withImages.Count == 0)
        {
            return new VoidCorpMissingImageBackfillResult(0, 0, Skipped: false);
        }

        foreach (var product in withImages)
        {
            VoidCorpCatalogSync.ClearProductImage(cacheRoot, product.Slug);
        }

        var result = await aiImageQueuePublisher.EnqueueVoidCorpProductsAsync(
            withImages.Select(product => product.Slug),
            $"voidcorp:{trigger}",
            cancellationToken);

        logger.LogInformation(
            "VoidCorp regenerate ({Trigger}) queued {Count} product image job(s).",
            trigger,
            result.EnqueuedCount);

        return new VoidCorpMissingImageBackfillResult(
            result.EnqueuedCount,
            0,
            Skipped: result.EnqueuedCount == 0);
    }

    private async Task<VoidCorpMissingImageBackfillResult> EnqueueMissingAsync(
        string trigger,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("VoidCorp backfill ({Trigger}) skipped: not configured.", trigger);
            return new VoidCorpMissingImageBackfillResult(0, 0, Skipped: true);
        }

        var settings = options.Value;
        VoidCorpCatalogSync.Sync(hostingPaths.VoidCorpCacheRoot, tradeItemsCatalog.GetSupplyItems());
        var document = VoidCorpCatalogSync.Load(hostingPaths.VoidCorpCacheRoot);
        var missing = VoidCorpMissingImageSelection.SelectMissing(
            hostingPaths.VoidCorpCacheRoot,
            document.Products,
            settings.MaxImagesPerDay);

        if (missing.Count == 0)
        {
            logger.LogDebug("VoidCorp backfill ({Trigger}): all product images present.", trigger);
            return new VoidCorpMissingImageBackfillResult(0, 0, Skipped: false);
        }

        var result = await aiImageQueuePublisher.EnqueueVoidCorpProductsAsync(
            missing.Select(product => product.Slug),
            $"voidcorp:{trigger}",
            cancellationToken);

        logger.LogInformation(
            "VoidCorp backfill ({Trigger}) queued {Count} product image job(s).",
            trigger,
            result.EnqueuedCount);

        return new VoidCorpMissingImageBackfillResult(
            result.EnqueuedCount,
            0,
            Skipped: result.EnqueuedCount == 0);
    }
}
