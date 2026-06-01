using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsEditionEnricherTests
{
    public OffworldNewsEditionEnricherTests()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "offworld-news-reporters.csv");
        OffworldNewsReporterCatalog.Configure(AppContext.BaseDirectory, Path.GetFileName(csvPath));
    }

    [Fact]
    public void EnrichAuthor_maps_unknown_ai_byline_to_roster_reporter()
    {
        var editionDate = new DateOnly(2026, 6, 1);
        var story = new OffworldNewsStoryDto(
            "ferroxite-yield",
            "Headline",
            "Dek",
            "Body",
            "Markets",
            "Sector 7",
            "Liam Galaxia",
            "liam-galaxia",
            editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            "Stellar Mining Co.",
            null);

        var enriched = OffworldNewsEditionEnricher.EnrichAuthor(story, editionDate, 0);

        Assert.NotEqual("liam-galaxia", enriched.AuthorSlug);
        Assert.NotEqual("Liam Galaxia", enriched.Author);
        Assert.NotNull(OffworldNewsReporterCatalog.TryGetBySlug(enriched.AuthorSlug));
        Assert.Equal(enriched.Author, OffworldNewsReporterCatalog.TryGetBySlug(enriched.AuthorSlug)!.DisplayName);
    }

    [Fact]
    public void EnrichAuthor_preserves_valid_roster_slug()
    {
        var editionDate = new DateOnly(2026, 6, 1);
        var story = new OffworldNewsStoryDto(
            "story-1",
            "Headline",
            "Dek",
            "Body",
            "Markets",
            "Ceres",
            "Mira Solano",
            "mira-solano",
            editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            null,
            null);

        var enriched = OffworldNewsEditionEnricher.EnrichAuthor(story, editionDate, 0);

        Assert.Equal("mira-solano", enriched.AuthorSlug);
        Assert.Equal("Mira Solano", enriched.Author);
    }
}
