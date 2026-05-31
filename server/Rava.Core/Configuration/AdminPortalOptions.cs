namespace Rava.Core.Configuration;

public class AdminPortalOptions
{
    public const string SectionName = "AdminPortal";

    public string PublicUrl { get; set; } = "https://ravaadmin.binarygeek119.duckdns.org/";

    public string GameUrl { get; set; } = "https://rava.binarygeek119.duckdns.org/";

    /// <summary>
    /// Internal API base URL for health checks from the admin host (optional).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
}
