namespace Theexonet.Core.Configuration;

public class AdminPortalOptions
{
    public const string SectionName = "AdminPortal";

    public string PublicUrl { get; set; } = "https://admin.theexonet.com/";

    public string GameUrl { get; set; } = "https://theexonet.com/";

    /// <summary>
    /// Internal API base URL for health checks from the admin host (optional).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
}
