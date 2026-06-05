using Rava.Core.Configuration;
using Rava.Core.Models;

namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed record ExonetAiAssetScanContext(
    RavaHostingPaths HostingPaths,
    string OffworldNewsReportersCsvPath,
    bool ForeverfallEnabled,
    bool OffworldNewsEnabled,
    bool LunarWeatherEnabled,
    bool VoidCorpEnabled,
    IReadOnlyList<TradeItemDefinition> SupplyItems);
