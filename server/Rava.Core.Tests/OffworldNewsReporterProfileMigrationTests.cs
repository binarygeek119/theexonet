using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterProfileMigrationTests
{
    [Fact]
    public void MergeReporter_preserves_existing_locations_and_fills_empty_appearance()
    {
        var existing = new OffworldNewsReporterProfile(
            "mira-solano",
            "Mira Solano",
            "Title",
            "Beat",
            "Bureau",
            "Personality",
            "Voice",
            "Directory",
            "ONN",
            "Kicker",
            ["specialty"],
            OffworldNewsReporterPortraitGender.Female,
            ["Custom embed only"],
            [],
            ReporterAppearance.Empty);

        var template = new OffworldNewsReporterProfile(
            "mira-solano",
            "Mira Solano",
            "Title",
            "Beat",
            "Bureau",
            "Personality",
            "Voice",
            "Directory",
            "ONN",
            "Kicker",
            ["specialty"],
            OffworldNewsReporterPortraitGender.Female,
            ["Template embed"],
            ["Template story"],
            new ReporterAppearance(
                "Template hair",
                "Template eyes",
                "Template race",
                "",
                "",
                "",
                "",
                ReporterSpecies.Human));

        var merged = OffworldNewsReporterProfileMigration.MergeReporter(existing, template);

        Assert.Single(merged.NotableLocations);
        Assert.Equal("Custom embed only", merged.NotableLocations[0]);
        Assert.Single(merged.NotableStories);
        Assert.Equal("Template story", merged.NotableStories[0]);
        Assert.Equal("Template hair", merged.Appearance.Hair);
        Assert.Equal("Template race", merged.Appearance.Race);
    }

    [Fact]
    public void MergeReporter_uses_template_lists_when_existing_empty()
    {
        var existing = new OffworldNewsReporterProfile(
            "jonah-kest",
            "Jonah Kest",
            "Title",
            "Beat",
            "Bureau",
            "Personality",
            "Voice",
            "Directory",
            "ONN",
            "Kicker",
            ["claims"],
            OffworldNewsReporterPortraitGender.Male);

        var template = existing with
        {
            NotableLocations = ["Mining drifts"],
            NotableStories = ["Drift safety walkback"],
            Appearance = new ReporterAppearance("Short black hair", "", "Weathered tan skin", "", "", "", "", ReporterSpecies.Human),
        };

        var merged = OffworldNewsReporterProfileMigration.MergeReporter(existing, template);

        Assert.Equal(["Mining drifts"], merged.NotableLocations);
        Assert.Equal(["Drift safety walkback"], merged.NotableStories);
        Assert.Equal("Short black hair", merged.Appearance.Hair);
    }

    [Fact]
    public void MergeReporter_fills_alien_species_from_template_without_overwriting_human_edits()
    {
        var existing = new OffworldNewsReporterProfile(
            "theo-brassard",
            "Theo Brassard",
            "Title",
            "Beat",
            "Bureau",
            "Personality",
            "Voice",
            "Directory",
            "ONN",
            "Kicker",
            ["surveys"],
            OffworldNewsReporterPortraitGender.Male,
            Appearance: new ReporterAppearance(
                "Custom crest",
                "",
                "Custom skin",
                "",
                "",
                "",
                "",
                ReporterSpecies.Human));

        var template = existing with
        {
            Appearance = new ReporterAppearance(
                "Template crest",
                "Template eyes",
                "Template skin",
                "",
                "",
                "",
                "Template features",
                "Europan"),
        };

        var merged = OffworldNewsReporterProfileMigration.MergeReporter(existing, template);

        Assert.Equal("Custom crest", merged.Appearance.Hair);
        Assert.Equal("Custom skin", merged.Appearance.Race);
        Assert.Equal("Europan", merged.Appearance.Species);
    }
}
