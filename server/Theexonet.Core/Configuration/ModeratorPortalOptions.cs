namespace Theexonet.Core.Configuration;

public class ModeratorPortalOptions
{
    public const string SectionName = "ModeratorPortal";

    public string PublicUrl { get; set; } = "https://theexonetmoderator.binarygeek119.duckdns.org/";

    public string GameUrl { get; set; } = "https://theexonet.binarygeek119.duckdns.org/";

    public string AdminPortalUrl { get; set; } = "https://theexonetadmin.binarygeek119.duckdns.org/";

    /// <summary>
    /// Internal API base URL for health checks from the moderator host (optional).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
}
