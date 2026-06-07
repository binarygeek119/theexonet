namespace Theexonet.Core.Configuration;

public class CompanyLogoOptions
{
    public const string SectionName = "CompanyLogo";

    public bool Enabled { get; set; } = true;

    /// <summary>When empty, uses <see cref="OpenAiOptions.BaseUrl"/>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>When empty, uses <see cref="OpenAiOptions.ImageModel"/>.</summary>
    public string ImageModel { get; set; } = string.Empty;

    /// <summary>Delay between queued logo generations to reduce API rate pressure.</summary>
    public int SecondsBetweenGenerations { get; set; } = 8;
}
