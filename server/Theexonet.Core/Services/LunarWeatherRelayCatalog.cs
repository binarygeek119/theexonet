using System.Text.RegularExpressions;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

/// <summary>Pool of 100 weather relays across the theexonet universe.</summary>
public static partial class LunarWeatherRelayCatalog
{
    private static readonly object Sync = new();
    private static IReadOnlyList<LunarWeatherRelayProfile> _all = BuildDefaultPool();
    private static string? _csvPath;

    public static IReadOnlyList<LunarWeatherRelayProfile> All
    {
        get
        {
            lock (Sync)
            {
                return _all;
            }
        }
    }

    public static void Configure(string contentRootPath, string relaysFile = "weather-relays.csv")
    {
        lock (Sync)
        {
            _csvPath = TheexonetDataPaths.ResolveFile(contentRootPath, relaysFile);
            _all = Load();
        }
    }

    public static LunarWeatherRelayProfile? TryGetById(string relayId) =>
        All.FirstOrDefault(relay => string.Equals(relay.Id, relayId, StringComparison.OrdinalIgnoreCase));

    public static LunarWeatherRelayDto ToDto(LunarWeatherRelayProfile relay) =>
        new(relay.Id, relay.Slug, relay.Name, relay.Region, relay.Sector, relay.BodyType);

    private static IReadOnlyList<LunarWeatherRelayProfile> Load()
    {
        if (_csvPath is not null && File.Exists(_csvPath))
        {
            var fromCsv = LunarWeatherRelaysCsvLoader.LoadFromFile(_csvPath);
            if (fromCsv.Count >= 100)
            {
                return fromCsv.Take(100).ToList();
            }
        }

        return BuildDefaultPool();
    }

    public static IReadOnlyList<LunarWeatherRelayProfile> BuildDefaultPool()
    {
        var facilities =
            new[]
            {
                "Met Array",
                "Skycrane Relay",
                "Dust Watch",
                "Plasma Mast",
                "Radiation Spire",
                "Particle Gate",
                "Magnetopause Node",
                "Coma Sensor",
                "Regolith Static Pad",
                "Vacuum Gauge Tower",
            };

        var anchors =
            new (string Name, string Region, string Sector, string BodyType)[]
            {
                ("Ceres Prime", "Main Belt", "Ceres Orbital", "asteroid_belt"),
                ("Vesta North Rim", "Main Belt", "Vesta L4", "asteroid_belt"),
                ("Pallas Darkside", "Main Belt", "Pallas Survey Lane", "asteroid_belt"),
                ("Hygeia Ice Shelf", "Main Belt", "Hygeia Outer", "asteroid_belt"),
                ("Luna Far Side", "Earth-Moon", "Selene Farside", "lunar_surface"),
                ("Luna Shackleton Rim", "Earth-Moon", "South Polar Basin", "lunar_surface"),
                ("Phobos Anchorage", "Mars System", "Phobos Low Orbit", "martian_moon"),
                ("Deimos Relay", "Mars System", "Deimos Escarpment", "martian_moon"),
                ("Olympus Mons Ledge", "Mars System", "Tharsis High", "planetary_surface"),
                ("Europa Fissure 7", "Jupiter System", "Europa Subsurface", "ice_moon"),
                ("Ganymede Magnetotail", "Jupiter System", "Ganymede Wake", "ice_moon"),
                ("Io Plasma Bridge", "Jupiter System", "Io Torus", "volcanic_moon"),
                ("Callisto Outer Rim", "Jupiter System", "Callisto Dust Halo", "ice_moon"),
                ("Titan Methane Deck", "Saturn System", "Titan Haze Layer", "haze_moon"),
                ("Enceladus Plume Lane", "Saturn System", "Enceladus Vent", "ice_moon"),
                ("Saturn Ring Plane", "Saturn System", "B Ring Shear", "ring_system"),
                ("Uranus Oblique Array", "Ice Giants", "Uranus Magnetopause", "ice_giant"),
                ("Neptune Storm Track", "Ice Giants", "Neptune Upper Deck", "ice_giant"),
                ("Triton Cryo Jet", "Neptune System", "Triton Retrograde", "cryo_moon"),
                ("Mercury Terminator", "Inner System", "Hermes Twilight", "rocky_planet"),
                ("Venus L2 Halo", "Inner System", "Aphrodite L2", "orbital_station"),
                ("Solar L1 Watch", "Inner System", "Sunward L1", "orbital_station"),
                ("Kuiper Survey Lane", "Outer System", "Kuiper Belt Node 4", "trans_neptunian"),
                ("Eris Perihelion", "Outer System", "Scattered Disk", "trans_neptunian"),
                ("Belt Sector 7", "Main Belt", "Sector 7 Aggregate", "asteroid_belt"),
                ("Halo-7 Watch Arc", "Main Belt", "Halo Survey", "asteroid_belt"),
                ("Driftward Expanse", "Main Belt", "Driftward Corridor", "asteroid_belt"),
                ("The Meridian Rift", "Deep Belt", "Rift Terminus", "deep_space"),
                ("Survey Planet K-19", "Frontier", "K-19 Observation", "frontier_world"),
                ("Orbital Commons Hub", "Politics Lane", "Charter Vote L4", "orbital_station"),
            };

        var relays = new List<LunarWeatherRelayProfile>(100);
        for (var index = 0; index < 100; index++)
        {
            var anchor = anchors[index % anchors.Length];
            var facility = facilities[index % facilities.Length];
            var id = $"LW-{index + 1:D3}";
            var name = $"{anchor.Name} {facility}";
            relays.Add(new LunarWeatherRelayProfile(
                id,
                Slugify(name),
                name,
                anchor.Region,
                anchor.Sector,
                anchor.BodyType));
        }

        return relays;
    }

    private static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        lower = NonSlugChars().Replace(lower, "-");
        lower = MultiDash().Replace(lower, "-").Trim('-');
        return lower.Length > 0 ? lower : "relay";
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"-{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiDash();
}
