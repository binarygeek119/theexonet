namespace Theexonet.Core.Services;

public static class CompanyNameGenerator
{
    private static readonly string[] Prefixes =
    [
        "Orion", "Stellar", "Nova", "Apex", "Deep", "Void", "Iron", "Quartz", "Nebula", "Titan",
        "Lunar", "Solar", "Aster", "Cosmo", "Ridge", "Basalt", "Cobalt", "Zenith", "Polar", "Meridian"
    ];

    private static readonly string[] Cores =
    [
        "Dig", "Vein", "Drill", "Ore", "Claim", "Mining", "Extract", "Core", "Fault", "Shaft",
        "Haul", "Forge", "Crust", "Bore", "Strike", "Load", "Span", "Reach", "Pulse", "Yield"
    ];

    private static readonly string[] Suffixes =
    [
        "Co.", "Corp", "Holdings", "Works", "Group", "Syndicate", "Industries", "Excavation", "Logistics", "Partners"
    ];

    /// <summary>Generates a sci-fi mining company name like "Orion Vein Works 472".</summary>
    public static string Generate()
    {
        var prefix = Prefixes[Random.Shared.Next(Prefixes.Length)];
        var core = Cores[Random.Shared.Next(Cores.Length)];
        var suffix = Suffixes[Random.Shared.Next(Suffixes.Length)];
        var number = Random.Shared.Next(10, 9999);
        return $"{prefix} {core} {suffix} {number}";
    }
}
