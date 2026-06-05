namespace Rava.Core.Configuration;

public class VoidCorpOptions
{
    public const string SectionName = "VoidCorp";

    public bool Enabled { get; set; } = true;

    /// <summary>Relative to the Exonet root. Stores catalog index and product images.</summary>
    public string CacheDirectory { get; set; } = "voidcorp";

    /// <summary>Maximum product images generated per UTC day when OpenAI is configured.</summary>
    public int MaxImagesPerDay { get; set; } = 4;
}
