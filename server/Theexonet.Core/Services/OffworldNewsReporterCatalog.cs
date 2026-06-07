using System.Security.Cryptography;
using System.Text;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public sealed record OffworldNewsReporterProfile(
    string Slug,
    string DisplayName,
    string Title,
    string Beat,
    string Bureau,
    string Personality,
    string WritingVoice,
    string DirectoryBio,
    string OnnBio,
    string StoryKicker,
    IReadOnlyList<string> Specialties,
    string Gender = OffworldNewsReporterPortraitGender.Male,
    IReadOnlyList<string> NotableLocations = null!,
    IReadOnlyList<string> NotableStories = null!,
    ReporterAppearance Appearance = null!)
{
    public IReadOnlyList<string> NotableLocations { get; init; } = NotableLocations ?? [];
    public IReadOnlyList<string> NotableStories { get; init; } = NotableStories ?? [];
    public ReporterAppearance Appearance { get; init; } = Appearance ?? ReporterAppearance.Empty;
}

/// <summary>
/// ONN reporter roster loaded from <c>offworld-news-reporters.csv</c> (editable in Excel or Google Sheets).
/// </summary>
public static class OffworldNewsReporterCatalog
{
    private static readonly object Gate = new();
    private static string? _csvPath;
    private static DateTime _cachedWriteUtc = DateTime.MinValue;
    private static IReadOnlyList<OffworldNewsReporterProfile> _all = [];
    private static Dictionary<string, OffworldNewsReporterProfile> _bySlug =
        new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, OffworldNewsReporterProfile> _byName =
        new(StringComparer.OrdinalIgnoreCase);
    private static int _storyPoolSize;

    public static void ConfigureStoryPoolSize(int reporterPoolSize)
    {
        lock (Gate)
        {
            _storyPoolSize = Math.Max(0, reporterPoolSize);
        }
    }

    public static int StoryPoolSize
    {
        get
        {
            lock (Gate)
            {
                return _storyPoolSize;
            }
        }
    }

    public static IReadOnlyList<OffworldNewsReporterProfile> StoryPool
    {
        get
        {
            var roster = All;
            if (_storyPoolSize <= 0 || _storyPoolSize >= roster.Count)
            {
                return roster;
            }

            return roster.Take(_storyPoolSize).ToList();
        }
    }

    public static void Configure(string contentRootPath, string reportersFile = "offworld-news-reporters.csv")
    {
        _csvPath = TheexonetDataPaths.ResolveFile(contentRootPath, reportersFile);
        Reload();
    }

    public static void Reload()
    {
        lock (Gate)
        {
            _cachedWriteUtc = DateTime.MinValue;
            EnsureLoaded(force: true);
        }
    }

    public static IReadOnlyList<OffworldNewsReporterProfile> All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }

    public static IReadOnlyList<string> DisplayNames =>
        All.Select(r => r.DisplayName).ToList();

    public static OffworldNewsReporterProfile? TryGetBySlug(string? slug)
    {
        EnsureLoaded();
        return slug is not null && _bySlug.TryGetValue(slug.Trim(), out var reporter) ? reporter : null;
    }

    public static OffworldNewsReporterProfile? TryGetByDisplayName(string? displayName)
    {
        EnsureLoaded();
        return displayName is not null && _byName.TryGetValue(displayName.Trim(), out var reporter) ? reporter : null;
    }

    /// <summary>Match roster slug, display name, handle (mira.solano), or slugified name.</summary>
    public static OffworldNewsReporterProfile? Resolve(string? slugOrNameOrHandle)
    {
        if (string.IsNullOrWhiteSpace(slugOrNameOrHandle))
        {
            return null;
        }

        var key = slugOrNameOrHandle.Trim();
        var bySlug = TryGetBySlug(key);
        if (bySlug is not null)
        {
            return bySlug;
        }

        var byName = TryGetByDisplayName(key);
        if (byName is not null)
        {
            return byName;
        }

        var handleAsSlug = key.Replace('.', '-');
        bySlug = TryGetBySlug(handleAsSlug);
        if (bySlug is not null)
        {
            return bySlug;
        }

        var underscored = key.Replace('_', '-');
        bySlug = TryGetBySlug(underscored);
        if (bySlug is not null)
        {
            return bySlug;
        }

        return TryGetBySlug(SlugifyDisplayName(key));
    }

    public static string SlugifyDisplayName(string displayName)
    {
        var slug = new StringBuilder();
        var pendingDash = false;
        foreach (var ch in displayName.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                slug.Append(ch);
                pendingDash = false;
                continue;
            }

            if (!pendingDash && slug.Length > 0)
            {
                slug.Append('-');
                pendingDash = true;
            }
        }

        return slug.ToString().Trim('-');
    }

    public static OffworldNewsReporterProfile PickForStory(DateOnly editionDate, int storyIndex)
    {
        var roster = StoryPool;
        if (roster.Count == 0)
        {
            throw new InvalidOperationException("Offworld News reporter roster is empty.");
        }

        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"offworld-news-author:{editionDate:yyyy-MM-dd}:{storyIndex}"));
        var slot = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        return roster[slot % roster.Count];
    }

    public static IReadOnlyList<OffworldNewsReporterProfile> PickReportersForEdition(DateOnly editionDate, int storyCount)
    {
        storyCount = Math.Clamp(storyCount, 1, 20);
        var reporters = new OffworldNewsReporterProfile[storyCount];
        for (var index = 0; index < storyCount; index++)
        {
            reporters[index] = PickForStory(editionDate, index);
        }

        return reporters;
    }

    public static string BuildWritingAssignmentBlock(IReadOnlyList<OffworldNewsReporterProfile> reporters)
    {
        var lines = new StringBuilder();
        for (var index = 0; index < reporters.Count; index++)
        {
            var reporter = reporters[index];
            lines.Append("- Story ")
                .Append(index + 1)
                .Append(": byline ")
                .Append(reporter.DisplayName)
                .Append(" — voice: ")
                .AppendLine(reporter.WritingVoice);
        }

        return lines.ToString().TrimEnd();
    }

    public static IReadOnlyList<OffworldNewsReporterProfile> Search(string? query, int limit = 20)
    {
        var roster = All;
        limit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(query))
        {
            return roster.Take(limit).ToList();
        }

        var terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roster
            .Select(reporter => (reporter, score: Score(reporter, terms)))
            .Where(pair => pair.score > 0)
            .OrderByDescending(pair => pair.score)
            .ThenBy(pair => pair.reporter.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(pair => pair.reporter)
            .ToList();
    }

    public static string HandleFromSlug(string slug) =>
        slug.Replace("-", ".", StringComparison.Ordinal);

    public static string DirectoryProfilePath(string slug) => OnnProfilePath(slug);

    public static string OnnProfilePath(string slug) => $"sites/offworld-news/reporters/{slug}";

    public static string DirectoryTeaser(OffworldNewsReporterProfile reporter)
    {
        var text = reporter.DirectoryBio.Trim();
        return text.Length <= 140 ? text : $"{text[..137].TrimEnd()}…";
    }

    public static OffworldNewsReporterDto ToDto(OffworldNewsReporterProfile reporter, params string[] assetRoots) =>
        new(
            reporter.Slug,
            reporter.DisplayName,
            HandleFromSlug(reporter.Slug),
            reporter.Title,
            reporter.Beat,
            reporter.Bureau,
            reporter.Personality,
            reporter.DirectoryBio,
            reporter.OnnBio,
            DirectoryTeaser(reporter),
            reporter.Specialties,
            assetRoots.Length > 0
                ? OffworldNewsReporterPaths.ResolveAvatarUrl(reporter.Slug, assetRoots)
                : OffworldNewsReporterPaths.AvatarUrl(reporter.Slug),
            assetRoots.Length > 0
                ? OffworldNewsReporterPaths.ResolveBackgroundUrl(reporter.Slug, assetRoots)
                : OffworldNewsReporterPaths.BackgroundUrl(reporter.Slug),
            DirectoryProfilePath(reporter.Slug),
            OnnProfilePath(reporter.Slug),
            OffworldNewsReporterBackgroundLocations.ProfileNote(reporter),
            reporter.NotableLocations,
            reporter.NotableStories);

    private static void EnsureLoaded(bool force = false)
    {
        if (_csvPath is null)
        {
            return;
        }

        if (!File.Exists(_csvPath))
        {
            throw new FileNotFoundException(
                $"Offworld News reporter roster not found: {_csvPath}. Add offworld-news-reporters.csv next to the API.",
                _csvPath);
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(_csvPath);
        if (!force && lastWriteUtc == _cachedWriteUtc && _all.Count > 0)
        {
            return;
        }

        lock (Gate)
        {
            if (!force && lastWriteUtc == _cachedWriteUtc && _all.Count > 0)
            {
                return;
            }

            var loaded = OffworldNewsReportersCsvLoader.LoadFromFile(_csvPath);
            if (loaded.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Offworld News reporter roster in {_csvPath} has no data rows.");
            }

            _all = loaded;
            _bySlug = loaded.ToDictionary(r => r.Slug, StringComparer.OrdinalIgnoreCase);
            _byName = loaded.ToDictionary(r => r.DisplayName, StringComparer.OrdinalIgnoreCase);
            _cachedWriteUtc = lastWriteUtc;
        }
    }

    private static int Score(OffworldNewsReporterProfile reporter, string[] terms)
    {
        var haystack = string.Join(
            ' ',
            reporter.DisplayName,
            HandleFromSlug(reporter.Slug),
            reporter.Title,
            reporter.Beat,
            reporter.Bureau,
            reporter.Personality,
            reporter.DirectoryBio,
            reporter.OnnBio,
            string.Join(' ', reporter.Specialties));

        var score = 0;
        foreach (var term in terms)
        {
            if (haystack.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += term.Length >= 4 ? 3 : 2;
            }

            if (reporter.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || reporter.Slug.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }
        }

        return score;
    }
}

public static class OffworldNewsReporterPaths
{
    public const string PublicReportersPath = "/exonet/offworld-news/reporters";

    public static string AvatarUrl(string slug) =>
        $"{PublicReportersPath}/{slug}/avatar.jpg";

    public static string BackgroundUrl(string slug) =>
        $"{PublicReportersPath}/{slug}/background.jpg";

    public static string ReporterFolder(string reportersRoot, string slug) =>
        Path.Combine(reportersRoot, slug);

    public static string AvatarFilePath(string reportersRoot, string slug) =>
        Path.Combine(ReporterFolder(reportersRoot, slug), "avatar.jpg");

    public static string BackgroundFilePath(string reportersRoot, string slug) =>
        Path.Combine(ReporterFolder(reportersRoot, slug), "background.jpg");

  /// <summary>
  /// Public URL for an asset when it exists under any of the given roots (newest root wins).
  /// Includes a cache-busting query from file mtime.
  /// </summary>
    public static string ResolveAvatarUrl(string slug, params string[] reportersRoots) =>
        ResolvePublicAssetUrl(slug, "avatar.jpg", reportersRoots);

    public static string ResolveBackgroundUrl(string slug, params string[] reportersRoots) =>
        ResolvePublicAssetUrl(slug, "background.jpg", reportersRoots);

    public static string ResolvePublicAssetUrl(string slug, string fileName, params string[] reportersRoots)
    {
        foreach (var root in reportersRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var filePath = Path.Combine(ReporterFolder(root, slug), fileName);
            if (!File.Exists(filePath))
            {
                continue;
            }

            var version = File.GetLastWriteTimeUtc(filePath).ToFileTimeUtc();
            return $"{PublicReportersPath}/{slug}/{fileName}?v={version}";
        }

        return $"{PublicReportersPath}/{slug}/{fileName}";
    }
}
