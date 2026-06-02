using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReportersCsvLoaderTests
{
    [Fact]
    public void SaveToFile_round_trips_reporter_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"onn-reporters-{Guid.NewGuid():N}.csv");
        var reporters = new[]
        {
            new OffworldNewsReporterProfile(
                "mira-solano",
                "Mira Solano",
                "Senior Markets Correspondent",
                "Markets",
                "Ceres Relay",
                "Calm numbers nerd",
                "Crisp, data-first sentences",
                "Directory bio with, comma",
                "ONN bio",
                "Kicker line",
                ["ore prices", "Rax flows"],
                OffworldNewsReporterPortraitGender.Female,
                ["Ceres Relay ore-spread gallery", "Belt terminal refinery bids"],
                ["The NPC Spread Panic That Wasn't"],
                new ReporterAppearance(
                    "Dark brown bob",
                    "Sharp hazel eyes",
                    "Latina; warm olive skin",
                    "Lean frame",
                    "None",
                    "Minimal makeup",
                    "Silver streak",
                    ReporterSpecies.Human)),
        };

        try
        {
            OffworldNewsReportersCsvLoader.SaveToFile(path, reporters);
            var loaded = OffworldNewsReportersCsvLoader.LoadFromFile(path);

            Assert.Single(loaded);
            Assert.Equal("mira-solano", loaded[0].Slug);
            Assert.Equal("Mira Solano", loaded[0].DisplayName);
            Assert.Equal("Directory bio with, comma", loaded[0].DirectoryBio);
            Assert.Equal(2, loaded[0].Specialties.Count);
            Assert.Equal(OffworldNewsReporterPortraitGender.Female, loaded[0].Gender);
            Assert.Equal(2, loaded[0].NotableLocations.Count);
            Assert.Equal("Ceres Relay ore-spread gallery", loaded[0].NotableLocations[0]);
            Assert.Single(loaded[0].NotableStories);
            Assert.Equal("Dark brown bob", loaded[0].Appearance.Hair);
            Assert.Equal("Latina; warm olive skin", loaded[0].Appearance.Race);
            Assert.Equal("Silver streak", loaded[0].Appearance.DistinctiveFeatures);
            Assert.Equal(ReporterSpecies.Human, loaded[0].Appearance.Species);
            Assert.True(OffworldNewsReportersCsvLoader.HasSpeciesColumn(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_legacy_twelve_column_rows_use_empty_extended_fields()
    {
        const string csv = """
            Slug,DisplayName,Title,Beat,Bureau,Personality,WritingVoice,DirectoryBio,OnnBio,StoryKicker,Specialties,Gender
            jonah-kest,Jonah Kest,Mining Reporter,Mining,Belt Sector 7,Skeptic,Short sentences,Dir bio,ONN bio,Kicker,claims; safety,male
            """;

        var reporters = OffworldNewsReportersCsvLoader.Parse(csv);

        Assert.Single(reporters);
        Assert.Equal("jonah-kest", reporters[0].Slug);
        Assert.Empty(reporters[0].NotableLocations);
        Assert.Empty(reporters[0].NotableStories);
        Assert.True(reporters[0].Appearance.IsEmpty);
        Assert.Equal(ReporterSpecies.Human, reporters[0].Appearance.Species);
    }

    [Fact]
    public void ParseDelimitedList_splits_semicolon_entries()
    {
        var items = OffworldNewsReportersCsvLoader.ParseDelimitedList(" Luna Port ; ;Refinery queue ; ");

        Assert.Equal(2, items.Count);
        Assert.Equal("Luna Port", items[0]);
        Assert.Equal("Refinery queue", items[1]);
    }

    [Fact]
    public void Parse_reads_alien_species_from_seed_csv()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "offworld-news-reporters.csv");
        var reporters = OffworldNewsReportersCsvLoader.LoadFromFile(csvPath);

        var europan = reporters.First(r => r.Slug == "theo-brassard");
        var callistan = reporters.First(r => r.Slug == "sable-nguyen");

        Assert.Equal("Europan", europan.Appearance.Species);
        Assert.Equal("Callistan", callistan.Appearance.Species);
        Assert.True(europan.Appearance.IsAlien);
        Assert.True(callistan.Appearance.IsAlien);
    }
}
