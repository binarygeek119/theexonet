using System.Text.Json;
using System.Text.Json.Serialization;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public static class VoidCorpCatalogSync
{
    private const string DefaultCategory = "Industrial Supply";
    private const string TemplateSource = "template";
    private const string OpenAiSource = "openai";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static VoidCorpCatalogSyncResult Sync(string cacheRoot, IReadOnlyList<TradeItemDefinition> supplies)
    {
        VoidCorpStoragePaths.EnsureDirectories(cacheRoot);

        var document = Load(cacheRoot);
        var bySlug = document.Products.ToDictionary(entry => entry.Slug, StringComparer.Ordinal);
        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var supply in supplies.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var slug = supply.ItemType;
            if (bySlug.TryGetValue(slug, out var existing))
            {
                var next = MergeExisting(existing, supply);
                if (EntriesEqual(existing, next))
                {
                    unchanged++;
                    bySlug[slug] = existing;
                    continue;
                }

                updated++;
                bySlug[slug] = next;
                continue;
            }

            var (summary, tagline, description) = VoidCorpProductTemplates.BuildCopy(supply.DisplayName, slug);
            bySlug[slug] = new VoidCorpCatalogEntryDocument(
                slug,
                supply.DisplayName,
                DefaultCategory,
                tagline,
                summary,
                description,
                supply.BasePrice,
                supply.Color,
                supply.UiSymbol,
                ImageFileName: null,
                TemplateSource);
            added++;
        }

        var nextDocument = new VoidCorpCatalogDocument(
            DateTime.UtcNow,
            bySlug.Values.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
        Save(cacheRoot, nextDocument);

        var missingImages = nextDocument.Products.Count(entry =>
            string.IsNullOrWhiteSpace(entry.ImageFileName)
            || !File.Exists(VoidCorpStoragePaths.ImageFilePath(cacheRoot, entry.Slug)));

        return new VoidCorpCatalogSyncResult(added, updated, unchanged, missingImages);
    }

    public static VoidCorpCatalogDocument Load(string cacheRoot)
    {
        var path = VoidCorpStoragePaths.CatalogPath(cacheRoot);
        if (!File.Exists(path))
        {
            return new VoidCorpCatalogDocument(DateTime.UtcNow, []);
        }

        try
        {
            return JsonSerializer.Deserialize<VoidCorpCatalogDocument>(File.ReadAllText(path), JsonOptions)
                ?? new VoidCorpCatalogDocument(DateTime.UtcNow, []);
        }
        catch (JsonException)
        {
            return new VoidCorpCatalogDocument(DateTime.UtcNow, []);
        }
    }

    public static void Save(string cacheRoot, VoidCorpCatalogDocument document)
    {
        VoidCorpStoragePaths.EnsureDirectories(cacheRoot);
        var path = VoidCorpStoragePaths.CatalogPath(cacheRoot);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    public static void ClearProductImage(string cacheRoot, string slug)
    {
        var filePath = VoidCorpStoragePaths.ImageFilePath(cacheRoot, slug);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var document = Load(cacheRoot);
        var products = document.Products.ToList();
        var index = products.FindIndex(entry => entry.Slug.Equals(slug, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var existing = products[index];
        products[index] = existing with { ImageFileName = null };
        Save(cacheRoot, document with
        {
            UpdatedAtUtc = DateTime.UtcNow,
            Products = products,
        });
    }

    public static void UpdateProductImage(string cacheRoot, string slug, string imageFileName, bool fromOpenAi)
    {
        var document = Load(cacheRoot);
        var products = document.Products.ToList();
        var index = products.FindIndex(entry => entry.Slug.Equals(slug, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var existing = products[index];
        products[index] = existing with
        {
            ImageFileName = imageFileName,
            Source = fromOpenAi ? OpenAiSource : existing.Source,
        };

        Save(cacheRoot, document with
        {
            UpdatedAtUtc = DateTime.UtcNow,
            Products = products,
        });
    }

    private static VoidCorpCatalogEntryDocument MergeExisting(
        VoidCorpCatalogEntryDocument existing,
        TradeItemDefinition supply)
    {
        var displayNameChanged = !string.Equals(existing.DisplayName, supply.DisplayName, StringComparison.Ordinal);
        var next = existing with
        {
            DisplayName = supply.DisplayName,
            BasePrice = supply.BasePrice,
            Color = supply.Color,
            UiSymbol = supply.UiSymbol,
        };

        if (!displayNameChanged || string.Equals(existing.Source, OpenAiSource, StringComparison.Ordinal))
        {
            return next;
        }

        var (summary, tagline, description) = VoidCorpProductTemplates.BuildCopy(supply.DisplayName, supply.ItemType);
        return next with
        {
            Tagline = tagline,
            Summary = summary,
            Description = description,
            Source = TemplateSource,
        };
    }

    private static bool EntriesEqual(VoidCorpCatalogEntryDocument left, VoidCorpCatalogEntryDocument right) =>
        left.Slug == right.Slug
        && left.DisplayName == right.DisplayName
        && left.Category == right.Category
        && left.Tagline == right.Tagline
        && left.Summary == right.Summary
        && left.Description == right.Description
        && left.BasePrice == right.BasePrice
        && left.Color == right.Color
        && left.UiSymbol == right.UiSymbol
        && left.ImageFileName == right.ImageFileName
        && left.Source == right.Source;
}
