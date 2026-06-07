using System.Text.Json.Nodes;
using Theexonet.Core.Configuration;

namespace Theexonet.Core.Tests;

public class AppSettingsTemplateMergerTests
{
    [Fact]
    public void MergeMissingProperties_adds_new_keys_without_overwriting_existing()
    {
        var template = JsonNode.Parse(
            """
            {
              "OffworldNews": {
                "Enabled": true,
                "ReportersFile": "offworld-news-reporters.csv"
              },
              "NewSection": { "Flag": true }
            }
            """) as JsonObject;

        var target = JsonNode.Parse(
            """
            {
              "OffworldNews": {
                "Enabled": false,
                "ApiKey": "secret-key"
              }
            }
            """) as JsonObject;

        var changed = AppSettingsTemplateMerger.MergeMissingProperties(template!, target!);

        Assert.True(changed);
        Assert.Equal("secret-key", target!["OffworldNews"]!["ApiKey"]!.GetValue<string>());
        Assert.False(target["OffworldNews"]!["Enabled"]!.GetValue<bool>());
        Assert.Equal(
            "offworld-news-reporters.csv",
            target["OffworldNews"]!["ReportersFile"]!.GetValue<string>());
        Assert.True(target["NewSection"]!["Flag"]!.GetValue<bool>());
    }

    [Fact]
    public void ApplyMissingKeys_updates_file_on_disk()
    {
        var root = Path.Combine(Path.GetTempPath(), $"theexonet-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var templatePath = Path.Combine(root, AppSettingsTemplateMerger.DefaultTemplateFileName);
        var targetPath = Path.Combine(root, "appsettings.json");
        File.WriteAllText(
            templatePath,
            """
            {
              "Alpha": { "One": 1 },
              "Beta": { "Two": 2 }
            }
            """);
        File.WriteAllText(
            targetPath,
            """
            {
              "Alpha": { "One": 99, "Keep": "yes" }
            }
            """);

        try
        {
            var updated = AppSettingsTemplateMerger.ApplyMissingKeys(root);
            Assert.Contains(targetPath, updated);

            var merged = JsonNode.Parse(File.ReadAllText(targetPath)) as JsonObject;
            Assert.Equal(99, merged!["Alpha"]!["One"]!.GetValue<int>());
            Assert.Equal("yes", merged["Alpha"]!["Keep"]!.GetValue<string>());
            Assert.Equal(2, merged["Beta"]!["Two"]!.GetValue<int>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
