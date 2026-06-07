using System.Text.Json;
using System.Text.Json.Serialization;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services.ExonetAiAssetScan;

public sealed class OffworldNewsImageScanner : IExonetAiAssetScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string AreaName => "OffworldNewsImages";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        if (!context.OffworldNewsEnabled)
        {
            return ExonetAiAssetScanAreaResult.SkippedArea(AreaName);
        }

        var cacheRoot = context.HostingPaths.OffworldNewsCacheRoot;
        var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = 0;

        foreach (var editionPath in OffworldNewsStoragePaths.EnumerateEditionFiles(cacheRoot))
        {
            OffworldNewsEditionDto? edition;
            try
            {
                edition = JsonSerializer.Deserialize<OffworldNewsEditionDto>(File.ReadAllText(editionPath), JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (edition is null)
            {
                continue;
            }

            foreach (var story in edition.Stories)
            {
                if (!OffworldNewsImagePaths.IsGeneratedImageUrl(story.ImageUrl))
                {
                    continue;
                }

                AddReferencedPaths(cacheRoot, story.ImageUrl, referencedPaths);

                if (!OffworldNewsImagePaths.GeneratedImageExists(cacheRoot, story.ImageUrl))
                {
                    missing++;
                }
            }
        }

        var orphans = 0;
        var imagesRoot = Path.Combine(cacheRoot, OffworldNewsStoragePaths.ImagesFolder);
        if (Directory.Exists(imagesRoot))
        {
            foreach (var imagePath in Directory.EnumerateFiles(imagesRoot, "*.jpg", SearchOption.AllDirectories))
            {
                if (!referencedPaths.Contains(imagePath))
                {
                    orphans++;
                }
            }
        }

        return new ExonetAiAssetScanAreaResult(
            AreaName,
            Orphans: orphans,
            Missing: missing);
    }

    private static void AddReferencedPaths(
        string cacheRoot,
        string? imageUrl,
        HashSet<string> referencedPaths)
    {
        if (!OffworldNewsImagePaths.TryParseGeneratedImageUrl(imageUrl, out var editionDate, out var fileName))
        {
            return;
        }

        referencedPaths.Add(Path.Combine(
            cacheRoot,
            OffworldNewsStoragePaths.ImagesFolder,
            OffworldNewsStoragePaths.ImageDayRelativePath(editionDate),
            fileName));

        referencedPaths.Add(Path.Combine(
            cacheRoot,
            OffworldNewsStoragePaths.ImagesFolder,
            editionDate.ToString("yyyy-MM-dd"),
            fileName));
    }
}
