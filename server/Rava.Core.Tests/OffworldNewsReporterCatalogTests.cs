using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterCatalogTests
{
    public OffworldNewsReporterCatalogTests()
    {
        ReporterCatalogTestSupport.ConfigureFromTestOutput();
    }

    [Fact]
    public void All_reporters_have_balanced_portrait_genders()
    {
        var femaleCount = OffworldNewsReporterCatalog.All
            .Count(r => r.Gender == OffworldNewsReporterPortraitGender.Female);
        var maleCount = OffworldNewsReporterCatalog.All
            .Count(r => r.Gender == OffworldNewsReporterPortraitGender.Male);

        Assert.Equal(8, femaleCount);
        Assert.Equal(7, maleCount);
    }

    [Fact]
    public void All_reporters_have_unique_slugs_and_handles()
    {
        Assert.Equal(15, OffworldNewsReporterCatalog.All.Count);
        Assert.Equal(
            OffworldNewsReporterCatalog.All.Count,
            OffworldNewsReporterCatalog.All.Select(r => r.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            OffworldNewsReporterCatalog.All.Count,
            OffworldNewsReporterCatalog.All.Select(r => OffworldNewsReporterCatalog.HandleFromSlug(r.Slug)).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Resolve_matches_slug_display_name_and_handle()
    {
        var bySlug = OffworldNewsReporterCatalog.Resolve("mira-solano");
        Assert.NotNull(bySlug);
        Assert.Equal("mira-solano", bySlug!.Slug);

        var byName = OffworldNewsReporterCatalog.Resolve("Mira Solano");
        Assert.NotNull(byName);
        Assert.Equal("mira-solano", byName!.Slug);

        var byHandle = OffworldNewsReporterCatalog.Resolve("mira.solano");
        Assert.NotNull(byHandle);
        Assert.Equal("mira-solano", byHandle!.Slug);
    }

    [Fact]
    public void Resolve_matches_underscore_slug_variants()
    {
        Assert.NotNull(OffworldNewsReporterCatalog.Resolve("mira_solano"));
    }

    [Fact]
    public void SlugifyDisplayName_matches_exonet_byline_rules()
    {
        Assert.Equal("mira-solano", OffworldNewsReporterCatalog.SlugifyDisplayName("Mira Solano"));
        Assert.Equal("jonah-kest", OffworldNewsReporterCatalog.SlugifyDisplayName("Jonah Kest"));
    }

    [Fact]
    public void Search_matches_handle_and_name()
    {
        var results = OffworldNewsReporterCatalog.Search("mira.solano", 5);
        Assert.Single(results);
        Assert.Equal("mira-solano", results[0].Slug);

        var byBeat = OffworldNewsReporterCatalog.Search("shipping", 10);
        Assert.Contains(byBeat, reporter => reporter.Beat == "Shipping");
    }

    [Fact]
    public void ToDto_includes_directory_and_onn_paths()
    {
        var dto = OffworldNewsReporterCatalog.ToDto(OffworldNewsReporterCatalog.All[0]);
        Assert.Equal("sites/offworld-news/reporters/mira-solano", dto.DirectoryProfilePath);
        Assert.Equal(dto.DirectoryProfilePath, dto.OnnProfilePath);
        Assert.False(string.IsNullOrWhiteSpace(dto.DirectoryTeaser));
        Assert.False(string.IsNullOrWhiteSpace(dto.Personality));
        Assert.False(string.IsNullOrWhiteSpace(dto.DirectoryBio));
        Assert.False(string.IsNullOrWhiteSpace(dto.OnnBio));
        Assert.NotEqual(dto.DirectoryBio, dto.OnnBio);
        Assert.StartsWith("Noteworthy embeds:", dto.ReportedLocationsNote);
        Assert.NotEmpty(dto.NotableLocations);
        Assert.NotEmpty(dto.NotableStories);
    }

    [Fact]
    public void BuildWritingAssignmentBlock_lists_each_story_voice()
    {
        var reporters = OffworldNewsReporterCatalog.PickReportersForEdition(new DateOnly(2026, 6, 1), 3);
        var block = OffworldNewsReporterCatalog.BuildWritingAssignmentBlock(reporters);

        Assert.Contains("Story 1:", block);
        Assert.Contains("Story 3:", block);
        Assert.Contains(reporters[0].DisplayName, block);
        Assert.Contains("voice:", block);
    }

    [Fact]
    public void PickForStory_uses_story_pool_when_configured()
    {
        OffworldNewsReporterCatalog.ConfigureStoryPoolSize(3);
        try
        {
            var poolSlugs = OffworldNewsReporterCatalog.StoryPool.Select(r => r.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Equal(3, poolSlugs.Count);

            for (var index = 0; index < 20; index++)
            {
                var picked = OffworldNewsReporterCatalog.PickForStory(new DateOnly(2026, 6, 1), index);
                Assert.Contains(picked.Slug, poolSlugs);
            }
        }
        finally
        {
            OffworldNewsReporterCatalog.ConfigureStoryPoolSize(0);
        }
    }
}
