namespace Rava.Core.Configuration;

public class CompanyLogoOptions
{
    public const string SectionName = "CompanyLogo";

    public bool Enabled { get; set; } = true;

    /// <summary>When empty, falls back to OffworldNews:ApiKey.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>GPT image models recommended for transparent PNG output.</summary>
    public string ImageModel { get; set; } = "gpt-image-1";

    /// <summary>Delay between queued logo generations to reduce API rate pressure.</summary>
    public int SecondsBetweenGenerations { get; set; } = 8;
}
