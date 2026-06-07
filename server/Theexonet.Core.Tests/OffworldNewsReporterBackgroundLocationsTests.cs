using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsReporterBackgroundLocationsTests
{
    public OffworldNewsReporterBackgroundLocationsTests()
    {
        ReporterCatalogTestSupport.ConfigureFromTestOutput();
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

        Assert.Contains(
            reporter!.NotableLocations,
            location => location.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        var primary = OffworldNewsReporterBackgroundLocations.PickPrimaryLocation(reporter);
        var scene = OffworldNewsReporterBackgroundLocations.DescribeScene(reporter);
        Assert.Contains(primary, scene, StringComparison.Ordinal);
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
    public void CareerStoriesNote_joins_notable_stories()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("marcus-whitaker");
        Assert.NotNull(reporter);

        var note = OffworldNewsReporterBackgroundLocations.CareerStoriesNote(reporter!);

        Assert.Contains("Morning Wrap That Moved Ferroxite", note);
        Assert.Contains("Buyback Alert at First Light", note);
    }

    [Fact]
    public void PickPrimaryLocation_is_stable_for_multiple_embeds()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("jonah-kest");
        Assert.NotNull(reporter);
        Assert.True(reporter!.NotableLocations.Count > 1);

        var first = OffworldNewsReporterBackgroundLocations.PickPrimaryLocation(reporter);
        var second = OffworldNewsReporterBackgroundLocations.PickPrimaryLocation(reporter);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
        Assert.Contains(first, reporter.NotableLocations, StringComparer.Ordinal);
    }

    [Fact]
    public void BuildBackgroundPrompt_uses_location_scene_and_bureau()
    {
        var reporter = OffworldNewsReporterCatalog.TryGetBySlug("marcus-whitaker");
        Assert.NotNull(reporter);

        var prompt = OffworldNewsReporterPortraitPrompts.BuildBackgroundPrompt(reporter);

        Assert.Contains("Titan Freight Hub", prompt);
        Assert.Contains("morning", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("news location", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no people", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
