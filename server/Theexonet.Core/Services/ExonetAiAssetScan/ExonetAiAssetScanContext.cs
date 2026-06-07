using Theexonet.Core.Configuration;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services.ExonetAiAssetScan;

public sealed record ExonetAiAssetScanContext(
    TheexonetHostingPaths HostingPaths,
    string OffworldNewsReportersCsvPath,
    bool ForeverfallEnabled,
    bool OffworldNewsEnabled,
    bool LunarWeatherEnabled,
    bool VoidCorpEnabled,
    IReadOnlyList<TradeItemDefinition> SupplyItems);
