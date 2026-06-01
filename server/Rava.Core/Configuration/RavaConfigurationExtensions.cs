using Microsoft.Extensions.Configuration;

namespace Rava.Core.Configuration;

public static class RavaConfigurationExtensions
{
    /// <summary>
    /// Loads appsettings from RAVA_DATA_DIR when it differs from the publish folder.
    /// </summary>
    public static IConfigurationBuilder AddRavaDataJsonFiles(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var dataRoot = RavaDataPaths.Resolve(contentRootPath);
        if (string.Equals(dataRoot, contentRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return configuration;
        }

        var appsettingsPath = Path.Combine(dataRoot, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: true);
        }

        var developmentPath = Path.Combine(dataRoot, "appsettings.Development.json");
        if (File.Exists(developmentPath))
        {
            configuration.AddJsonFile(developmentPath, optional: true, reloadOnChange: true);
        }

        return configuration;
    }
}
