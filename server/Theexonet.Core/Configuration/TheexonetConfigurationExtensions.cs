using Microsoft.Extensions.Configuration;

namespace Theexonet.Core.Configuration;

public static class TheexonetConfigurationExtensions
{
    /// <summary>
    /// Loads appsettings from THEEXONET_DATA_DIR when it differs from the publish folder.
    /// </summary>
    public static IConfigurationBuilder AddTheexonetDataJsonFiles(
        this IConfigurationBuilder configuration,
        string contentRootPath)
    {
        var dataRoot = TheexonetDataPaths.Resolve(contentRootPath);
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
