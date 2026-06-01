using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterCatalogTests
{
    public OffworldNewsReporterCatalogTests()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "offworld-news-reporters.csv");
        OffworldNewsReporterCatalog.Configure(AppContext.BaseDirectory, Path.GetFileName(csvPath));
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
        Assert.Equal("reporters/mira-solano", dto.DirectoryProfilePath);
        Assert.Equal("sites/offworld-news/reporters/mira-solano", dto.OnnProfilePath);
        Assert.False(string.IsNullOrWhiteSpace(dto.DirectoryTeaser));
        Assert.False(string.IsNullOrWhiteSpace(dto.Personality));
        Assert.False(string.IsNullOrWhiteSpace(dto.DirectoryBio));
        Assert.False(string.IsNullOrWhiteSpace(dto.OnnBio));
        Assert.NotEqual(dto.DirectoryBio, dto.OnnBio);
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
}
