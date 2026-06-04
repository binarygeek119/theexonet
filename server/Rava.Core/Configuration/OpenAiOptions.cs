namespace Rava.Core.Configuration;

/// <summary>Shared OpenAI credentials and defaults for all game AI features.</summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional organization admin key (sk-admin-...) for month-to-date spend on the status dashboard.
    /// Cannot be used for game AI calls.
    /// </summary>
    public string AdminApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string TextModel { get; set; } = "gpt-4o-mini";

    /// <summary>DALL-E 3 default. GPT image models (gpt-image-1, …) are also supported.</summary>
    public string ImageModel { get; set; } = "dall-e-3";
}
