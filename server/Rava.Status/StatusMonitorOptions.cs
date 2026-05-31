namespace Rava.Status;

public class StatusMonitorOptions
{
    public const string SectionName = "StatusMonitor";

    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public string GameUrl { get; set; } = "https://rava.binarygeek119.duckdns.org/";
    public string ApiPublicUrl { get; set; } = "https://ravaapi.binarygeek119.duckdns.org/";
    public string StatusPublicUrl { get; set; } = "https://ravastatus.binarygeek119.duckdns.org/";
    public string DocsInternalUrl { get; set; } = "http://127.0.0.1:9000";
    public string DocsPublicUrl { get; set; } = "https://ravadocs.binarygeek119.duckdns.org/";
}
