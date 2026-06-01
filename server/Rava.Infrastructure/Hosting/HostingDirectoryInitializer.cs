using Microsoft.Extensions.Logging;
using Rava.Core.Configuration;
using Rava.Infrastructure.Services;

namespace Rava.Infrastructure.Hosting;

/// <summary>
/// Creates required hosting folders and applies Unix permissions on Linux production servers.
/// </summary>
public static class HostingDirectoryInitializer
{
    public const string DefaultWwwRoot = "/var/www";
    public const string DefaultPublishRoot = "/var/www/publish";

    private const UnixFileMode ReadExecuteDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode WritableDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode WritableSetGroupDirectoryMode =
        WritableDirectoryMode | UnixFileMode.SetGroup;

    public static void Ensure(
        string contentRootPath,
        string webRootPath,
        string imagesRootPath,
        string offworldNewsCacheRoot,
        ILogger logger)
    {
        var dataRoot = RavaDataPaths.Resolve(contentRootPath);
        var usingExternalData =
            !string.Equals(dataRoot, contentRootPath, StringComparison.OrdinalIgnoreCase);

        if (usingExternalData && OperatingSystem.IsLinux())
        {
            EnsureProductionLinuxDirectories(
                contentRootPath,
                dataRoot,
                offworldNewsCacheRoot,
                logger);
        }
        else
        {
            EnsureWritableApplicationDirectories(imagesRootPath, offworldNewsCacheRoot);
        }
    }

    private static void EnsureProductionLinuxDirectories(
        string contentRootPath,
        string dataRoot,
        string offworldNewsCacheRoot,
        ILogger logger)
    {
        var wwwRoot = ResolveWwwRoot(dataRoot);
        var directories = new (string Path, UnixFileMode Mode, bool Required)[]
        {
            (wwwRoot, ReadExecuteDirectoryMode, false),
            (dataRoot, WritableSetGroupDirectoryMode, true),
            (contentRootPath, ReadExecuteDirectoryMode, false),
            (Path.Combine(contentRootPath, ".aspnet"), WritableDirectoryMode, false),
            (Path.Combine(dataRoot, "images"), WritableSetGroupDirectoryMode, true),
            (Path.Combine(dataRoot, "images", ProfileAvatarStorageOptions.RelativeFolder), WritableSetGroupDirectoryMode, true),
            (Path.Combine(dataRoot, "images", ProfileBackgroundStorageOptions.RelativeFolder), WritableSetGroupDirectoryMode, true),
            (Path.Combine(dataRoot, "exonet"), WritableSetGroupDirectoryMode, true),
            (Path.Combine(offworldNewsCacheRoot, "editions"), WritableSetGroupDirectoryMode, true),
            (Path.Combine(offworldNewsCacheRoot, "images"), WritableSetGroupDirectoryMode, true),
        };

        foreach (var (path, mode, required) in directories)
        {
            EnsureDirectory(path, mode, required, logger);
        }
    }

    private static void EnsureWritableApplicationDirectories(
        string imagesRootPath,
        string offworldNewsCacheRoot)
    {
        Directory.CreateDirectory(Path.Combine(imagesRootPath, ProfileAvatarStorageOptions.RelativeFolder));
        Directory.CreateDirectory(Path.Combine(imagesRootPath, ProfileBackgroundStorageOptions.RelativeFolder));
        Directory.CreateDirectory(Path.Combine(offworldNewsCacheRoot, "editions"));
        Directory.CreateDirectory(Path.Combine(offworldNewsCacheRoot, "images"));
    }

    private static string ResolveWwwRoot(string dataRoot)
    {
        var parent = Directory.GetParent(dataRoot.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        return string.IsNullOrWhiteSpace(parent) ? DefaultWwwRoot : parent;
    }

    private static void EnsureDirectory(string path, UnixFileMode mode, bool required, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(path);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(path, mode);
            }
            logger.LogInformation("Hosting directory ready: {Path} ({Mode})", path, FormatMode(mode));
        }
        catch (Exception ex) when (!required)
        {
            logger.LogWarning(
                ex,
                "Could not set permissions on {Path}. If uploads fail, run: sudo chown -R www-data:www-data /var/www/data",
                path);
        }
        catch (Exception ex) when (required)
        {
            throw new InvalidOperationException(
                $"Required hosting directory is missing or not writable: {path}. " +
                $"Ensure www-data can write under {RavaDataPaths.DefaultProductionPath}.",
                ex);
        }
    }

    private static string FormatMode(UnixFileMode mode) =>
        Convert.ToString((int)mode & 0xFFF, 8).PadLeft(4, '0');
}
