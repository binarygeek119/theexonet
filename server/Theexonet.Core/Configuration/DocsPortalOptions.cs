namespace Theexonet.Core.Configuration;

public class DocsPortalOptions
{
    public const string SectionName = "DocsPortal";

    public string PublicUrl { get; set; } = "https://docs.theexonet.com/";

    public string GameUrl { get; set; } = "https://theexonet.com/";

    public string ContentPath { get; set; } = "content";

    public string SiteTitle { get; set; } = "theexonet Game Docs";
}
