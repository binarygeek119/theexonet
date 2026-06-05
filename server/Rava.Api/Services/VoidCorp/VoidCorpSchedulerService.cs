using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Services;

namespace Rava.Api.Services.VoidCorp;

/// <summary>
/// Generates missing VoidCorp product images after startup and at UTC midnight.
/// </summary>
public sealed class VoidCorpSchedulerService(
    VoidCorpProductImageGenerator imageGenerator,
    RavaHostingPaths hostingPaths,
    ITradeItemsCatalog tradeItemsCatalog,
    IOptions<VoidCorpOptions> options,
    ILogger<VoidCorpSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("VoidCorp scheduler disabled.");
            return;
        }

        await RunCycleAsync("startup", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UtcGameClock.NextDayBoundaryUtc - DateTime.UtcNow + TimeSpan.FromSeconds(3);
            if (delay < TimeSpan.FromSeconds(1))
            {
                delay = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunCycleAsync("midnight", stoppingToken);
        }
    }

    private async Task RunCycleAsync(string trigger, CancellationToken cancellationToken)
    {
        if (!imageGenerator.IsConfigured)
        {
            logger.LogDebug("VoidCorp scheduler ({Trigger}) skipped: OpenAI not configured.", trigger);
            return;
        }

        var settings = options.Value;
        VoidCorpCatalogSync.Sync(hostingPaths.VoidCorpCacheRoot, tradeItemsCatalog.GetSupplyItems());
        var document = VoidCorpCatalogSync.Load(hostingPaths.VoidCorpCacheRoot);
        var missing = document.Products
            .Where(product =>
                string.IsNullOrWhiteSpace(product.ImageFileName)
                || !File.Exists(VoidCorpStoragePaths.ImageFilePath(hostingPaths.VoidCorpCacheRoot, product.Slug)))
            .Take(Math.Max(1, settings.MaxImagesPerDay))
            .ToList();

        if (missing.Count == 0)
        {
            logger.LogDebug("VoidCorp scheduler ({Trigger}): all product images present.", trigger);
            return;
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
            "VoidCorp scheduler ({Trigger}) finished: generated {Generated}/{Attempted} missing images",
            trigger,
            generated,
            missing.Count);
    }
}
