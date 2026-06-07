namespace Theexonet.Core.Services.ExonetAiAssetScan;

public interface IExonetAiAssetScanner
{
    string AreaName { get; }

    ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context);
}
