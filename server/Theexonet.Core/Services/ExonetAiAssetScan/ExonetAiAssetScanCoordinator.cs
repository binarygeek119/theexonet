namespace Theexonet.Core.Services.ExonetAiAssetScan;

public static class ExonetAiAssetScanCoordinator
{
    private static readonly IExonetAiAssetScanner[] Scanners =
    [
        new ForeverfallPortraitRegistryScanner(),
        new OffworldNewsImageScanner(),
        new OffworldNewsReporterScanner(),
        new LunarWeatherEditionScanner(),
        new VoidCorpCatalogScanner(),
        new TestingDummyFriendsScanner(),
    ];

    public static ExonetAiAssetScanSummary RunAll(ExonetAiAssetScanContext context)
    {
        var results = new List<ExonetAiAssetScanAreaResult>(Scanners.Length);
        foreach (var scanner in Scanners)
        {
            results.Add(scanner.Scan(context));
        }

        return new ExonetAiAssetScanSummary(results);
    }
}
