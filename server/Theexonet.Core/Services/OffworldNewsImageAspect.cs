using System.Security.Cryptography;
using System.Text;

namespace Theexonet.Core.Services;

public readonly record struct OffworldNewsImageAspect(
    string Key,
    string ApiSize,
    string CompositionHint);

/// <summary>
/// Supported DALL-E 3 output sizes and stable per-story aspect selection.
/// </summary>
public static class OffworldNewsImageAspectCatalog
{
    public static readonly IReadOnlyList<OffworldNewsImageAspect> All =
    [
        new(
            "landscape",
            "1792x1024",
            "Wide horizontal landscape news photograph with cinematic widescreen framing."),
        new(
            "square",
            "1024x1024",
            "Balanced square news photograph composition."),
        new(
            "portrait",
            "1024x1792",
            "Tall vertical portrait news photograph with dramatic editorial height."),
    ];

    public static OffworldNewsImageAspect Pick(DateOnly editionDate, string storyId, int storyIndex)
    {
        if (All.Count == 0)
        {
            throw new InvalidOperationException("Offworld News image aspect catalog is empty.");
        }

        var hash = ComputeHash($"{editionDate:yyyy-MM-dd}:{storyId}:{storyIndex}");
        return All[hash % All.Count];
    }

    public static OffworldNewsImageAspect? FindByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        foreach (var aspect in All)
        {
            if (string.Equals(aspect.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return aspect;
            }
        }

        return null;
    }

    public static string ResolveApiSize(string? aspectKey, string fallbackSize = "1024x1024") =>
        FindByKey(aspectKey)?.ApiSize ?? fallbackSize;

    private static int ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }
}
