namespace Theexonet.Core.Configuration;

/// <summary>
/// Seeds CSV and other data files from the publish folder into <see cref="TheexonetDataPaths"/> when missing.
/// Never overwrites files already present in the data directory.
/// </summary>
public static class TheexonetDataFileBootstrap
{
    /// <summary>
    /// Ensures <paramref name="fileName"/> exists under the resolved data root, copying from
    /// <paramref name="contentRootPath"/> when the publish copy exists and the data copy does not.
    /// </summary>
    /// <returns>Resolved path under the data root.</returns>
    public static string EnsureFromPublish(string contentRootPath, string fileName)
    {
        var dataPath = TheexonetDataPaths.ResolveFile(contentRootPath, fileName);
        if (File.Exists(dataPath))
        {
            return dataPath;
        }

        var publishPath = Path.Combine(Path.GetFullPath(contentRootPath), fileName);
        if (File.Exists(publishPath)
            && !string.Equals(
                Path.GetFullPath(publishPath),
                Path.GetFullPath(dataPath),
                StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(publishPath, dataPath, overwrite: false);
            Console.WriteLine($"Data file: seeded missing {fileName} from publish -> {dataPath}");
            return dataPath;
        }

        if (File.Exists(publishPath))
        {
            return publishPath;
        }

        return dataPath;
    }
}
