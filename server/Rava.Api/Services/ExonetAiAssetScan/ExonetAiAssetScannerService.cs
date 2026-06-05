using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Core.Services.ExonetAiAssetScan;

namespace Rava.Api.Services.ExonetAiAssetScan;

/// <summary>
/// Reconciles Exonet AI assets on startup and at each UTC midnight.
/// </summary>
public sealed class ExonetAiAssetScannerService(
    IOptions<ExonetAiAssetScannerOptions> scannerOptions,
    IOptions<ForeverfallOptions> foreverfallOptions,
    IOptions<OffworldNewsOptions> offworldNewsOptions,
    IOptions<LunarWeatherOptions> lunarWeatherOptions,
    IOptions<VoidCorpOptions> voidCorpOptions,
    ITradeItemsCatalog tradeItemsCatalog,
    RavaHostingPaths hostingPaths,
    ILogger<ExonetAiAssetScannerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!scannerOptions.Value.Enabled)
        {
            logger.LogInformation("Exonet AI asset scanner disabled.");
            return;
        }

        await RunScanAsync("startup", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UtcGameClock.NextDayBoundaryUtc - DateTime.UtcNow + TimeSpan.FromSeconds(1);
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

            await RunScanAsync("midnight", stoppingToken);
        }
    }

    private Task RunScanAsync(string trigger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reportersFile = offworldNewsOptions.Value.ReportersFile;
        var reportersCsvPath = Path.Combine(hostingPaths.DataRoot, reportersFile);

        var context = new ExonetAiAssetScanContext(
            hostingPaths,
            reportersCsvPath,
            foreverfallOptions.Value.Enabled,
            offworldNewsOptions.Value.Enabled,
            lunarWeatherOptions.Value.Enabled,
            voidCorpOptions.Value.Enabled,
            tradeItemsCatalog.GetSupplyItems());

        var summary = ExonetAiAssetScanCoordinator.RunAll(context);
        LogSummary(trigger, summary);

        return Task.CompletedTask;
    }

    private void LogSummary(string trigger, ExonetAiAssetScanSummary summary)
    {
        foreach (var area in summary.Areas)
        {
            if (area.Skipped)
            {
                logger.LogDebug("Exonet AI asset scan ({Trigger}): {Area} skipped (feature disabled).", trigger, area.AreaName);
                continue;
            }

            logger.LogInformation(
                "Exonet AI asset scan ({Trigger}): {Area} imported={Imported}, registered={Registered}, invalid={Invalid}, orphans={Orphans}, missing={Missing}",
                trigger,
                area.AreaName,
                area.Imported,
                area.AlreadyRegistered,
                area.SkippedInvalid + area.Invalid,
                area.Orphans,
                area.Missing);
        }
    }
}
