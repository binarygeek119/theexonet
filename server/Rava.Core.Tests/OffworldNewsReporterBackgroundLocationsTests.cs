using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterBackgroundLocationsTests
{
    public OffworldNewsReporterBackgroundLocationsTests()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "offworld-news-reporters.csv");
        OffworldNewsReporterCatalog.Configure(AppContext.BaseDirectory, Path.GetFileName(csvPath));
    }

    [Fact]
    public void Every_roster_reporter_has_a_distinct_location_scene()
    {
        var scenes = OffworldNewsReporterCatalog.All
            .Select(reporter => OffworldNewsReporterBackgroundLocations.DescribeScene(reporter))
            .ToList();

        Assert.Equal(15, scenes.Count);
        Assert.Equal(15, scenes.ToHashSet(StringComparer.Ordinal).Count);
    }

    [Theory]
    [InlineData("jonah-kest", "mining drift")]
    [InlineData("priya-menon", "Luna Port")]
    [InlineData("sable-nguyen", "Exonet")]
    [InlineData("theo-brassard", "Europa Deep Survey")]
    [InlineData("zara-pemberton", "upload-queue")]
    public void Known_reporters_reference_signature_reporting_locations(string slug, string fragment)
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug(slug);
        Assert.NotNull(reporter);

        var scene = OffworldNewsReporterBackgroundLocations.DescribeScene(reporter);
        Assert.Contains(fragment, scene, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfileNote_lists_noteworthy_embeds_for_every_reporter()
    {
        var notes = OffworldNewsReporterCatalog.All
            .Select(reporter => OffworldNewsReporterBackgroundLocations.ProfileNote(reporter))
            .ToList();

        Assert.Equal(15, notes.Count);
        Assert.All(notes, note => Assert.StartsWith("Noteworthy embeds:", note));
        Assert.Equal(15, notes.ToHashSet(StringComparer.Ordinal).Count);
    }

    [Theory]
    [InlineData("jonah-kest", "mining drifts")]
    [InlineData("marcus-whitaker", "Titan Freight Hub")]
    public void ProfileNote_references_signature_locations(string slug, string fragment)
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug(slug);
        Assert.NotNull(reporter);

        var note = OffworldNewsReporterBackgroundLocations.ProfileNote(reporter);
        Assert.Contains(fragment, note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBackgroundPrompt_uses_location_scene_and_bureau()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("marcus-whitaker");
        Assert.NotNull(reporter);

        var prompt = OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter);

        Assert.Contains("Titan Freight Hub", prompt);
        Assert.Contains("morning market wrap", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("news location", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no people", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
