namespace Theexonet.Core.Configuration;

public class HostingOptions
{
    public const string SectionName = "Hosting";

    /// <summary>
    /// When false, GET / shows an API status page instead of the game UI.
    /// Omit in config to default to true in Development and false in Production.
    /// </summary>
    public bool? ServeGameUi { get; set; }
}
