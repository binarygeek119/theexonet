using Rava.Core.Configuration;

namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed record ExonetAiAssetScanContext(
    RavaHostingPaths HostingPaths,
    string OffworldNewsReportersCsvPath,
    bool ForeverfallEnabled,
    bool OffworldNewsEnabled,
    bool LunarWeatherEnabled);
