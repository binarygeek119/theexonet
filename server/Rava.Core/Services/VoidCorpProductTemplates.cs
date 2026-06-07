using Rava.Core.Enums;

namespace Rava.Core.Services;

public static class VoidCorpProductTemplates
{
    private static readonly Dictionary<SupplyType, (string Summary, string Tagline)> KnownEffects =
        new()
        {
            [SupplyType.DrillBits] = (
                "Boosts mining speed",
                "Precision-cut consumables for high-throughput extraction"),
            [SupplyType.FuelCells] = (
                "Powers hauling operations",
                "Dense orbital fuel packs for cargo and shuttle cycles"),
            [SupplyType.LifeSupport] = (
                "Keeps workers efficient",
                "Closed-loop life support modules for extended belt shifts"),
            [SupplyType.CommModules] = (
                "Improves management systems",
                "Relay-grade comm stacks for mine coordination and telemetry"),
        };

    public static (string Summary, string Tagline, string Description) BuildCopy(
        string displayName,
        string? itemTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(itemTypeKey)
            && Enum.TryParse<SupplyType>(itemTypeKey, true, out var supplyType)
            && KnownEffects.TryGetValue(supplyType, out var known))
        {
            return (
                known.Summary,
                known.Tagline,
                BuildDescription(displayName, known.Summary, known.Tagline));
        }

        var fallbackSummary = "Supports frontier mining operations";
        var fallbackTagline = "Industrial-grade equipment for belt contractors";
        return (
            fallbackSummary,
            fallbackTagline,
            BuildDescription(displayName, fallbackSummary, fallbackTagline));
    }

    private static string BuildDescription(string displayName, string summary, string tagline) =>
        $"{displayName} is manufactured by VoidCorp for licensed asteroid mining contractors. "
        + $"{tagline}. In field use, this product {summary.ToLowerInvariant()}. "
        + "Specifications meet VoidCorp frontier reliability standards; availability through "
        + "authorized theexonet supply channels may vary by relay lag and market conditions.";
}
