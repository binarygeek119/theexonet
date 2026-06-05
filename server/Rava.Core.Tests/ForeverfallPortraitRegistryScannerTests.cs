using System.Text.Json;
using System.Text.Json.Serialization;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;
using Rava.Core.Services.ExonetAiAssetScan;

namespace Rava.Core.Tests;

public class ForeverfallPortraitRegistryScannerTests
{
    [Fact]
    public void Sync_imports_unregistered_portraits_and_updates_next_number()
    {
        var root = CreateTempDirectory();
        try
        {
            var imagesDir = Path.Combine(root, ForeverfallStoragePaths.ImagesFolder);
            Directory.CreateDirectory(imagesDir);
            File.WriteAllText(Path.Combine(imagesDir, "FF-0001.jpg"), "one");
            File.WriteAllText(Path.Combine(imagesDir, "FF-0002.jpg"), "two");
            File.WriteAllText(Path.Combine(imagesDir, "not-valid.jpg"), "skip");

            var result = ForeverfallPortraitRegistryScanner.Sync(root);

            Assert.Equal(2, result.Imported);
            Assert.Equal(0, result.AlreadyRegistered);
            Assert.Equal(1, result.SkippedInvalid);

            var registryPath = ForeverfallStoragePaths.ImageRegistryPath(root);
            Assert.True(File.Exists(registryPath));

            var registry = JsonSerializer.Deserialize<ForeverfallImageRegistry>(
                File.ReadAllText(registryPath),
                JsonOptions)!;

            Assert.Equal(2, registry.Images.Count);
            Assert.Equal(3, registry.NextImageNumber);
            Assert.Contains(registry.Images, entry => entry.ImageId == "FF-0001");
            Assert.Contains(registry.Images, entry => entry.ImageId == "FF-0002");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Sync_skips_already_registered_portraits()
    {
        var root = CreateTempDirectory();
        try
        {
            ForeverfallStoragePaths.EnsureDirectories(root);
            var imagesDir = Path.Combine(root, ForeverfallStoragePaths.ImagesFolder);
            File.WriteAllText(Path.Combine(imagesDir, "FF-0001.jpg"), "one");
            File.WriteAllText(Path.Combine(imagesDir, "FF-0002.jpg"), "two");

            var existing = new ForeverfallImageRegistry(
            [
                new ForeverfallImageRegistryEntry("FF-0001", "male", DateTime.UtcNow, "FF-0001.jpg"),
            ],
            NextImageNumber: 2);

            File.WriteAllText(
                ForeverfallStoragePaths.ImageRegistryPath(root),
                JsonSerializer.Serialize(existing, JsonOptions));

            var result = ForeverfallPortraitRegistryScanner.Sync(root);

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.AlreadyRegistered);

            var registry = JsonSerializer.Deserialize<ForeverfallImageRegistry>(
                File.ReadAllText(ForeverfallStoragePaths.ImageRegistryPath(root)),
                JsonOptions)!;

            Assert.Equal(2, registry.Images.Count);
            Assert.Equal(3, registry.NextImageNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"ff-scan-{Guid.NewGuid():N}");
}
