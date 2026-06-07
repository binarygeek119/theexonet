using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.VoidCorp;

public sealed record VoidCorpMissingImageBackfillResult(int Attempted, int Generated, bool Skipped);

public sealed class VoidCorpMissingImageBackfillService(
    VoidCorpProductImageGenerator imageGenerator,
    TheexonetHostingPaths hostingPaths,
    ITradeItemsCatalog tradeItemsCatalog,
    IOptions<VoidCorpOptions> options,
    ILogger<VoidCorpMissingImageBackfillService> logger)
{
    private static readonly SemaphoreSlim RunLock = new(1, 1);

    public bool IsConfigured => imageGenerator.IsConfigured && options.Value.Enabled;

    public void EnqueueAfterSync(VoidCorpCatalogSyncResult syncResult)
    {
        if (!IsConfigured || !VoidCorpImageBackfillPolicy.ShouldEnqueueAfterSync(syncResult))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAsync("sync", CancellationToken.None, waitForLock: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VoidCorp sync backfill failed.");
            }
        });
    }

    public Task<VoidCorpMissingImageBackfillResult> RunAsync(
        string trigger,
        CancellationToken cancellationToken,
        bool waitForLock = true)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("VoidCorp backfill ({Trigger}) skipped: not configured.", trigger);
            return Task.FromResult(new VoidCorpMissingImageBackfillResult(0, 0, Skipped: true));
        }

        return waitForLock
            ? RunWithLockAsync(trigger, cancellationToken, waitForLock: true)
            : RunWithLockAsync(trigger, cancellationToken, waitForLock: false);
    }

    private async Task<VoidCorpMissingImageBackfillResult> RunWithLockAsync(
        string trigger,
        CancellationToken cancellationToken,
        bool waitForLock)
    {
        var acquired = waitForLock
            ? await AcquireLockAsync(cancellationToken)
            : await RunLock.WaitAsync(0, cancellationToken);

        if (!acquired)
        {
            logger.LogDebug("VoidCorp backfill ({Trigger}) skipped: another run in progress.", trigger);
            return new VoidCorpMissingImageBackfillResult(0, 0, Skipped: true);
        }

        try
        {
            return await GenerateMissingAsync(trigger, cancellationToken);
        }
        finally
        {
            RunLock.Release();
        }
    }

    private async Task<bool> AcquireLockAsync(CancellationToken cancellationToken)
    {
        await RunLock.WaitAsync(cancellationToken);
        return true;
    }

    private async Task<VoidCorpMissingImageBackfillResult> GenerateMissingAsync(
        string trigger,
        CancellationToken cancellationToken)
    {
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

        var generated = 0;
        foreach (var product in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (ok, error) = await imageGenerator.GenerateAndSaveAsync(product, cancellationToken);
            if (ok)
            {
                generated++;
                logger.LogInformation("VoidCorp generated product image for {Slug} ({Trigger})", product.Slug, trigger);
            }
            else
            {
                logger.LogWarning(
                    "VoidCorp product image generation failed for {Slug} ({Trigger}): {Error}",
                    product.Slug,
                    trigger,
                    error);
            }
        }

        logger.LogInformation(
            "VoidCorp backfill ({Trigger}) finished: generated {Generated}/{Attempted} missing images",
            trigger,
            generated,
            missing.Count);

        return new VoidCorpMissingImageBackfillResult(missing.Count, generated, Skipped: false);
    }
}
