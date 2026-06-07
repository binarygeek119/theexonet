namespace Theexonet.Core.Configuration;

public class ModeratorPortalOptions
{
    public const string SectionName = "ModeratorPortal";

    public string PublicUrl { get; set; } = "https://moderator.theexonet.com/";

    public string GameUrl { get; set; } = "https://theexonet.com/";

    public string AdminPortalUrl { get; set; } = "https://admin.theexonet.com/";

    /// <summary>
    /// Internal API base URL for health checks from the moderator host (optional).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
}
