namespace Rava.Core.Configuration;

public class OffworldNewsOptions
{
    public const string SectionName = "OffworldNews";

    public bool Enabled { get; set; } = true;

    /// <summary>OpenAI (or compatible) API key. When empty, template headlines are used instead.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string TextModel { get; set; } = "gpt-4o-mini";

    /// <summary>DALL-E 3 default. GPT image models (gpt-image-1, gpt-image-1.5, …) are also supported.</summary>
    public string ImageModel { get; set; } = "dall-e-3";

    public int StoriesPerDay { get; set; } = 5;

    /// <summary>Up to this many stories per day receive an AI illustration (remainder use placeholders).</summary>
    public int MaxImagesPerDay { get; set; } = 5;

    /// <summary>Relative to the Exonet root (data/exonet or html/exonet). Stores generated editions and images.</summary>
    public string CacheDirectory { get; set; } = "offworld-news";
}
