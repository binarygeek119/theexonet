using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rava.Core.Configuration;

/// <summary>
/// Adds keys from <c>appsettings.production.example.json</c> into live settings files without overwriting existing values.
/// </summary>
public static class AppSettingsTemplateMerger
{
    public const string DefaultTemplateFileName = "appsettings.production.example.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Merges missing keys from the template into appsettings.json (and appsettings.json.example) when those files exist.
    /// </summary>
    public static IReadOnlyList<string> ApplyMissingKeys(
        string contentRootPath,
        string templateFileName = DefaultTemplateFileName)
    {
        var templatePath = Path.Combine(contentRootPath, templateFileName);
        if (!File.Exists(templatePath))
        {
            return [];
        }

        var templateNode = JsonNode.Parse(File.ReadAllText(templatePath));
        if (templateNode is not JsonObject template)
        {
            return [];
        }

        var updated = new List<string>();
        foreach (var targetPath in ResolveTargetPaths(contentRootPath))
        {
            if (TryMergeFile(template, targetPath))
            {
                updated.Add(targetPath);
            }
        }

        return updated;
    }

    public static bool MergeMissingProperties(JsonObject template, JsonObject target)
    {
        var changed = false;
        foreach (var property in template)
        {
            var key = property.Key;
            var templateValue = property.Value;
            if (templateValue is null)
            {
                continue;
            }

            if (!target.TryGetPropertyValue(key, out var existing) || existing is null)
            {
                target[key] = templateValue.DeepClone();
                changed = true;
                continue;
            }

            if (templateValue is JsonObject templateObject && existing is JsonObject existingObject)
            {
                if (MergeMissingProperties(templateObject, existingObject))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static IEnumerable<string> ResolveTargetPaths(string contentRootPath)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfExists(string path)
        {
            if (!File.Exists(path) || !seen.Add(path))
            {
                return;
            }

            paths.Add(path);
        }

        AddIfExists(Path.Combine(contentRootPath, "appsettings.json"));
        AddIfExists(Path.Combine(contentRootPath, "appsettings.json.example"));

        var dataRoot = RavaDataPaths.Resolve(contentRootPath);
        if (!string.Equals(dataRoot, contentRootPath, StringComparison.OrdinalIgnoreCase))
        {
            AddIfExists(Path.Combine(dataRoot, "appsettings.json"));
        }

        return paths;
    }

    private static bool TryMergeFile(JsonObject template, string targetPath)
    {
        JsonObject target;
        if (File.Exists(targetPath))
        {
            var parsed = JsonNode.Parse(File.ReadAllText(targetPath));
            target = parsed as JsonObject ?? new JsonObject();
        }
        else
        {
            return false;
        }

        if (!MergeMissingProperties(template, target))
        {
            return false;
        }

        File.WriteAllText(targetPath, target.ToJsonString(WriteOptions) + Environment.NewLine);
        return true;
    }
}
