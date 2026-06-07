using Theexonet.Core.Enums;

namespace Theexonet.Core.Services;

public static class VoidCorpProductPrompts
{
    public static string BuildImagePrompt(
        string slug,
        string displayName,
        string summary,
        string tagline,
        string? accentColor = null)
    {
        var subject = BuildVisualSubject(slug, displayName);
        var accent = DescribeAccentColor(accentColor);
        return
            $"Professional product photograph of {subject}. "
            + $"VoidCorp frontier manufacturer aesthetic. {tagline}. "
            + $"Function: {summary}. {accent}"
            + "Hard-science sci-fi, studio lighting on dark brushed metal surface, "
            + "photorealistic catalog hero shot, no text, no logos, no watermarks, no people.";
    }

    private static string BuildVisualSubject(string slug, string displayName)
    {
        if (!Enum.TryParse<SupplyType>(slug, true, out var supplyType))
        {
            return $"industrial asteroid mining supply hardware for {displayName}";
        }

        return supplyType switch
        {
            SupplyType.DrillBits =>
                $"tungsten-carbide rotary drill bits, replaceable cutting heads, and threaded mining consumables "
                + $"for {displayName} — show fluted cutters, worn carbide teeth, and a tray of interchangeable bit cartridges",
            SupplyType.FuelCells =>
                $"sealed high-density fuel cell modules and orbital propellant cartridges for {displayName} — "
                + "show rectangular power packs, pressure valves, hazard striping, and fuel containment housings",
            SupplyType.LifeSupport =>
                $"closed-loop life support hardware for {displayName} — O2 scrubber canisters, filter cartridges, "
                + "pressure regulators, breathable-atmosphere modules, hoses, and gauge manifolds",
            SupplyType.CommModules =>
                $"relay-grade communications hardware for {displayName} — compact telemetry transceivers, mesh relay boxes, "
                + "folded antenna arrays, RF ports, and signal booster modules",
            _ => $"industrial asteroid mining supply hardware for {displayName}",
        };
    }

    private static string DescribeAccentColor(string? accentColor)
    {
        if (string.IsNullOrWhiteSpace(accentColor))
        {
            return string.Empty;
        }

        return $"Subtle product accent lighting and trim in {accentColor.Trim()} tones matching VoidCorp catalog branding. ";
    }
}
