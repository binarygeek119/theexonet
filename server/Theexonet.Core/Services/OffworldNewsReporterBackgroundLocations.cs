using System.Security.Cryptography;
using System.Text;

namespace Theexonet.Core.Services;

/// <summary>
/// Reporter-specific embed locations and career-story context for banner AI and public profiles.
/// </summary>
public static class OffworldNewsReporterBackgroundLocations
{
    public static string DescribeScene(OffworldNewsReporterProfile reporter)
    {
        var location = PickPrimaryLocation(reporter);
        var memorabilia = FormatStoryMemorabilia(reporter);
        if (location.Length == 0)
        {
            location = BuildFallbackScene(reporter);
        }

        if (memorabilia.Length == 0)
        {
            return location;
        }

        return $"{location}. Bureau personalization from career highlights: {memorabilia}";
    }

    public static string ProfileNote(OffworldNewsReporterProfile reporter)
    {
        if (reporter.NotableLocations.Count > 0)
        {
            return $"Noteworthy embeds: {string.Join(", ", reporter.NotableLocations)}.";
        }

        return
            $"Noteworthy embeds across {reporter.Bureau.Trim()} and other {reporter.Beat.Trim().ToLowerInvariant()} beats on the belt relay.";
    }

    public static string CareerStoriesNote(OffworldNewsReporterProfile reporter)
    {
        if (reporter.NotableStories.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", reporter.NotableStories);
    }

    public static string PickPrimaryLocation(OffworldNewsReporterProfile reporter)
    {
        if (reporter.NotableLocations.Count == 0)
        {
            return string.Empty;
        }

        if (reporter.NotableLocations.Count == 1)
        {
            return reporter.NotableLocations[0];
        }

        var index = StableIndex(reporter.Slug, reporter.NotableLocations.Count);
        return reporter.NotableLocations[index];
    }

    private static string FormatStoryMemorabilia(OffworldNewsReporterProfile reporter)
    {
        if (reporter.NotableStories.Count == 0)
        {
            return string.Empty;
        }

        var props = reporter.NotableStories
            .Select(story =>
                $"framed headline clippings, award plaques, or monitor stills referencing \"{story.Trim()}\"")
            .ToList();

        return string.Join("; ", props);
    }

    private static string BuildFallbackScene(OffworldNewsReporterProfile reporter)
    {
        var specialty = reporter.Specialties.FirstOrDefault() ?? reporter.Beat;
        return
            $"signature ONN filing spot at {reporter.Bureau.Trim()}, " +
            $"view and props tied to {reporter.Beat.Trim()} coverage and {specialty}, " +
            "empty press workstation and equipment ready for a live dispatch";
    }

    private static int StableIndex(string slug, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(slug.Trim().ToLowerInvariant()));
        return (int)(BitConverter.ToUInt32(hash, 0) % (uint)count);
    }
}
