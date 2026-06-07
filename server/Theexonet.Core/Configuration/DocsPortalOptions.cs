namespace Theexonet.Core.Configuration;

public class DocsPortalOptions
{
    public const string SectionName = "DocsPortal";

    public string PublicUrl { get; set; } = "https://theexonetdocs.binarygeek119.duckdns.org/";

    public string GameUrl { get; set; } = "https://theexonet.binarygeek119.duckdns.org/";

    public string ContentPath { get; set; } = "content";

    public string SiteTitle { get; set; } = "theexonet Game Docs";
}
