using Microsoft.Extensions.Options;
using Theexonet.Api.Services.OpenAi;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Services.VoidCorp;

public sealed class VoidCorpAdminService(
    TheexonetHostingPaths hostingPaths,
    ITradeItemsCatalog tradeItemsCatalog,
    VoidCorpMissingImageBackfillService backfillService,
    VoidCorpProductImageGenerator imageGenerator,
    OpenAiConnectionResolver openAi,
    IOptions<VoidCorpOptions> voidCorpOptions)
{
    public AdminVoidCorpStatusDto GetStatus()
    {
        var settings = voidCorpOptions.Value;
        var cacheRoot = hostingPaths.VoidCorpCacheRoot;

        if (settings.Enabled)
        {
            VoidCorpCatalogSync.Sync(cacheRoot, tradeItemsCatalog.GetSupplyItems());
        }

        var document = VoidCorpCatalogSync.Load(cacheRoot);
        var productCount = document.Products.Count;
        var missingImages = productCount == 0
            ? 0
            : document.Products.Count(product => VoidCorpMissingImageSelection.IsMissing(cacheRoot, product));
        var withImages = productCount - missingImages;

        return new AdminVoidCorpStatusDto(
            settings.Enabled,
            openAi.IsApiKeyConfigured,
            settings.Enabled && openAi.IsApiKeyConfigured,
            productCount,
            withImages,
            missingImages,
            Math.Max(1, settings.MaxImagesPerDay),
            document.UpdatedAtUtc);
    }

    public async Task<(AdminVoidCorpGenerateImagesResponse? Response, string? Error)> GenerateMissingImagesAsync(
        CancellationToken cancellationToken)
    {
        var settings = voidCorpOptions.Value;
        if (!settings.Enabled)
        {
            return (null, "VoidCorp is disabled in configuration.");
        }

        if (!imageGenerator.IsConfigured)
        {
            return (null, "OpenAi.ApiKey is not configured; AI product images are unavailable.");
        }

        var statusBefore = GetStatus();
        if (statusBefore.MissingImagesCount == 0)
        {
            return (new AdminVoidCorpGenerateImagesResponse(
                "All product images are already present.",
                0,
                0,
                0), null);
        }

        var result = await backfillService.RunAsync("admin", cancellationToken);
        if (result.Attempted == 0)
        {
            return (new AdminVoidCorpGenerateImagesResponse(
                result.Skipped
                    ? "VoidCorp image generation is not configured."
                    : "All product images are already present.",
                0,
                0,
                statusBefore.MissingImagesCount), null);
        }

        var statusAfter = GetStatus();
        var message = result.Attempted > 0
            ? $"Queued {result.Attempted} missing product image(s) for generation."
            : result.Attempted > 0
                ? "Image generation attempted but no images were saved. Check API logs for details."
                : "All product images are already present.";

        return (new AdminVoidCorpGenerateImagesResponse(
            message,
            result.Attempted,
            result.Generated,
            statusAfter.MissingImagesCount), null);
    }

    public async Task<(AdminVoidCorpGenerateImagesResponse? Response, string? Error)> RegenerateImagesAsync(
        CancellationToken cancellationToken)
    {
        var settings = voidCorpOptions.Value;
        if (!settings.Enabled)
        {
            return (null, "VoidCorp is disabled in configuration.");
        }

        if (!imageGenerator.IsConfigured)
        {
            return (null, "OpenAi.ApiKey is not configured; AI product images are unavailable.");
        }

        var statusBefore = GetStatus();
        if (statusBefore.WithImagesCount == 0)
        {
            return (null, "No product images found to regenerate.");
        }

        var result = await backfillService.RegenerateExistingAsync("admin:regenerate-images", cancellationToken);
        if (result.Attempted == 0)
        {
            return (null, "No product images found to regenerate.");
        }

        var statusAfter = GetStatus();
        return (new AdminVoidCorpGenerateImagesResponse(
            $"Queued {result.Attempted} product image(s) for regeneration.",
            result.Attempted,
            0,
            statusAfter.MissingImagesCount), null);
    }
}
