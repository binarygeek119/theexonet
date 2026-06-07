namespace Theexonet.Core.Configuration;

public class OffworldNewsOptions
{
    public const string SectionName = "OffworldNews";

    public bool Enabled { get; set; } = true;

    /// <summary>Target stories per edition; actual count varies daily (see variance settings).</summary>
    public int StoriesPerDay { get; set; } = 5;

    /// <summary>Fuzzy variance around <see cref="StoriesPerDay"/> (triangular distribution, date-seeded).</summary>
    public int StoriesPerDayVariance { get; set; } = 3;

    public int MinStoriesPerDay { get; set; } = 1;

    public int MaxStoriesPerDay { get; set; } = 10;

    /// <summary>Up to this many stories per day receive an AI illustration (remainder use placeholders).</summary>
    public int MaxImagesPerDay { get; set; } = 5;

    /// <summary>Relative to the Exonet root (data/exonet or html/exonet). Stores generated editions and images.</summary>
    public string CacheDirectory { get; set; } = "offworld-news";

    /// <summary>Reporter personalities and writing voices (opens in Excel or Google Sheets).</summary>
    public string ReportersFile { get; set; } = "offworld-news-reporters.csv";
}
