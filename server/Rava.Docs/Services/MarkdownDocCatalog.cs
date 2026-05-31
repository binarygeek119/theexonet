using System.Text.RegularExpressions;

namespace Rava.Docs.Services;

public sealed record MarkdownDocEntry(string Slug, string Title, string RelativePath);

public sealed class MarkdownDocCatalog
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly string _contentRoot;
    private MarkdownDocEntry[] _entries = [];

    public MarkdownDocCatalog(string contentRoot)
    {
        _contentRoot = contentRoot;
        Reload();
    }

    public IReadOnlyList<MarkdownDocEntry> Entries => _entries;

    public bool TryResolve(string? slug, out MarkdownDocEntry? entry, out string? markdown)
    {
        entry = null;
        markdown = null;

        var normalizedSlug = NormalizeSlug(slug);
        if (normalizedSlug is null)
        {
            return false;
        }

        entry = _entries.FirstOrDefault(item => item.Slug == normalizedSlug);
        if (entry is null)
        {
            return false;
        }

        var path = Path.Combine(_contentRoot, entry.RelativePath);
        if (!File.Exists(path))
        {
            entry = null;
            return false;
        }

        markdown = File.ReadAllText(path);
        return true;
    }

    public void Reload()
    {
        if (!Directory.Exists(_contentRoot))
        {
            _entries = [];
            return;
        }

        _entries = Directory
            .EnumerateFiles(_contentRoot, "*.md", SearchOption.AllDirectories)
            .Select(ToEntry)
            .Where(item => item is not null)
            .Cast<MarkdownDocEntry>()
            .OrderBy(item => item.Slug == "index" ? "" : item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private MarkdownDocEntry ToEntry(string absolutePath)
    {
        var relativePath = Path.GetRelativePath(_contentRoot, absolutePath).Replace('\\', '/');
        var slug = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();
        var markdown = File.ReadAllText(absolutePath);
        var title = ExtractTitle(markdown) ?? ToTitle(slug);
        return new MarkdownDocEntry(slug, title, relativePath);
    }

    private static string? NormalizeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return "index";
        }

        slug = slug.Trim().Trim('/').ToLowerInvariant();
        return SlugPattern.IsMatch(slug) ? slug : null;
    }

    private static string? ExtractTitle(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                return trimmed.TrimStart('#').Trim();
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }
        }

        return null;
    }

    private static string ToTitle(string slug) =>
        string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
