namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed record ExonetAiAssetScanAreaResult(
    string AreaName,
    int Imported = 0,
    int AlreadyRegistered = 0,
    int SkippedInvalid = 0,
    int Orphans = 0,
    int Missing = 0,
    int Invalid = 0,
    bool Skipped = false)
{
    public static ExonetAiAssetScanAreaResult SkippedArea(string areaName) =>
        new(areaName, Skipped: true);
}
