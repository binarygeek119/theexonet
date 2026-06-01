namespace Rava.Core.Configuration;

public class OffworldNewsOptions
{
    public const string SectionName = "OffworldNews";

    public bool Enabled { get; set; } = true;

    /// <summary>OpenAI (or compatible) API key. When empty, template headlines are used instead.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string TextModel { get; set; } = "gpt-4o-mini";

    public string ImageModel { get; set; } = "dall-e-3";

    public int StoriesPerDay { get; set; } = 5;

    /// <summary>Up to this many stories per day may receive an AI illustration.</summary>
    public int MaxImagesPerDay { get; set; } = 2;

    /// <summary>Relative to WebRoot (html/). Generated editions and images are stored here.</summary>
    public string CacheDirectory { get; set; } = "exonet/offworld-news";
}
