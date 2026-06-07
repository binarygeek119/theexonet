namespace Rava.Core.Configuration;

public class DocsPortalOptions
{
    public const string SectionName = "DocsPortal";

    public string PublicUrl { get; set; } = "https://ravadocs.binarygeek119.duckdns.org/";

    public string GameUrl { get; set; } = "https://rava.binarygeek119.duckdns.org/";

    public string ContentPath { get; set; } = "content";

    public string SiteTitle { get; set; } = "theexonet Game Docs";
}
