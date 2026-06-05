namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed record ExonetAiAssetScanSummary(IReadOnlyList<ExonetAiAssetScanAreaResult> Areas)
{
    public static ExonetAiAssetScanSummary Empty { get; } = new([]);
}
