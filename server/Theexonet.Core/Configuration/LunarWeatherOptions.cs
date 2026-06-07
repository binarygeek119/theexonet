namespace Theexonet.Core.Configuration;

public class LunarWeatherOptions
{
    public const string SectionName = "LunarWeather";

    public bool Enabled { get; set; } = true;

    /// <summary>Relative to the Exonet root. Stores generated daily bulletins.</summary>
    public string CacheDirectory { get; set; } = "lunar-weather";

    public string RelaysFile { get; set; } = "weather-relays.csv";

    public int RelayPoolSize { get; set; } = 100;

    /// <summary>Target number of relays reporting at midnight (fuzzy variance applied).</summary>
    public int TargetOperationalCount { get; set; } = 30;

    public int OperationalVariance { get; set; } = 5;

    public int MinOperationalCount { get; set; } = 22;

    public int MaxOperationalCount { get; set; } = 38;
}
