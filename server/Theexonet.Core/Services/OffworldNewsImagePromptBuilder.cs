using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public static class OffworldNewsImagePromptBuilder
{
    private const int MaxBodyExcerptLength = 420;

    /// <summary>
    /// Builds a DALL-E prompt tied to a specific story. Prefer <paramref name="aiImagePrompt"/>
    /// from the story writer when available; otherwise derive from headline, dek, body, and metadata.
    /// </summary>
    public static string Build(
        OffworldNewsStoryDto story,
        string? aiImagePrompt = null,
        string? compositionHint = null)
    {
        var prompt = !string.IsNullOrWhiteSpace(aiImagePrompt)
            ? aiImagePrompt.Trim()
            : BuildStoryDescription(story);

        return WrapVisualDescription(prompt, compositionHint);
    }

    private static string BuildStoryDescription(OffworldNewsStoryDto story)
    {
        var parts = new List<string>
        {
            $"News illustration for the story \"{story.Headline.Trim()}\".",
            story.Dek.Trim(),
        };

        var bodyExcerpt = ExtractBodyExcerpt(story.Body);
        if (!string.IsNullOrWhiteSpace(bodyExcerpt))
        {
            parts.Add($"Story scene: {bodyExcerpt}");
        }

        if (!string.IsNullOrWhiteSpace(story.Location))
        {
            parts.Add($"Setting: {story.Location.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(story.Category))
        {
            parts.Add($"Topic: {story.Category.Trim()} coverage.");
        }

        if (!string.IsNullOrWhiteSpace(story.CompanyName))
        {
            parts.Add($"Featured operator: {story.CompanyName.Trim()}.");
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static string ExtractBodyExcerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var plain = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\n', ' ')
            .Replace("  ", " ", StringComparison.Ordinal);

        while (plain.Contains("  ", StringComparison.Ordinal))
        {
            plain = plain.Replace("  ", " ", StringComparison.Ordinal);
        }

        plain = plain.Trim();
        if (plain.Length <= MaxBodyExcerptLength)
        {
            return plain;
        }

        var truncated = plain[..MaxBodyExcerptLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > MaxBodyExcerptLength / 2)
        {
            truncated = truncated[..lastSpace];
        }

        return truncated.TrimEnd('.', ' ') + "…";
    }

    private static string WrapVisualDescription(string description, string? compositionHint = null)
    {
        var framing = string.IsNullOrWhiteSpace(compositionHint)
            ? "Cinematic composition"
            : compositionHint.Trim();

        return "Editorial sci-fi news photograph for the Offworld News Network, asteroid-mining frontier economy. " +
               "Depict the specific scene described below — not a generic space vista. " +
               "No text, no logos, no words, no watermarks, no captions. " +
               $"{framing}, believable industrial hardware. " +
               "Color grade: strong cool blue and cyan tint throughout, deep-space editorial look, steel-blue shadows, icy cyan highlights, subdued warm tones. " +
               description;
    }
}
