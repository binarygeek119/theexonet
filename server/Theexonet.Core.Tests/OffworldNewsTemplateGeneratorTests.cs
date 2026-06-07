using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsTemplateGeneratorTests
{
    public OffworldNewsTemplateGeneratorTests()
    {
        ReporterCatalogTestSupport.ConfigureFromTestOutput();
    }

    [Fact]
    public void Generate_produces_five_stories_for_a_fixed_date()
    {
        var edition = OffworldNewsTemplateGenerator.Generate(new DateOnly(2026, 5, 29), 5);

        Assert.Equal(new DateOnly(2026, 5, 29), edition.EditionDate);
        Assert.Equal("template", edition.Source);
        Assert.Equal(5, edition.Stories.Count);
        Assert.All(edition.Stories, story =>
        {
            Assert.False(string.IsNullOrWhiteSpace(story.Id));
        Assert.False(string.IsNullOrWhiteSpace(story.Headline));
        Assert.False(string.IsNullOrWhiteSpace(story.Body));
        Assert.Contains("\n\n", story.Body);
        Assert.False(string.IsNullOrWhiteSpace(story.CompanyName));
        Assert.False(string.IsNullOrWhiteSpace(story.ImageUrl));
        });
    }

    [Fact]
    public void Generate_is_deterministic_for_the_same_date()
    {
        var date = new DateOnly(2026, 1, 15);
        var first = OffworldNewsTemplateGenerator.Generate(date, 5);
        var second = OffworldNewsTemplateGenerator.Generate(date, 5);

        Assert.Equal(first.Stories.Select(s => s.Headline), second.Stories.Select(s => s.Headline));
    }

    [Fact]
    public void PlaceholderImageForCategory_maps_story_types_to_placeholders()
    {
        Assert.Contains("frontier", OffworldNewsTemplateGenerator.PlaceholderImageForCategory("New Planets"));
        Assert.Contains("politics", OffworldNewsTemplateGenerator.PlaceholderImageForCategory("Politics"));
        Assert.Contains("markets", OffworldNewsTemplateGenerator.PlaceholderImageForCategory("Stocks"));
    }

    [Fact]
    public void Generate_includes_all_core_story_types_for_five_story_editions()
    {
        var edition = OffworldNewsTemplateGenerator.Generate(new DateOnly(2026, 6, 7), 5);

        Assert.Equal(OffworldNewsEditionStoryTypes.Core, edition.Stories.Select(story => story.Category));
    }

    [Fact]
    public void Generate_can_include_supplemental_types_for_larger_editions()
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var day = 0; day < 40; day++)
        {
            var edition = OffworldNewsTemplateGenerator.Generate(new DateOnly(2026, 6, 1).AddDays(day), 8);
            foreach (var story in edition.Stories)
            {
                categories.Add(story.Category);
            }
        }

        Assert.Contains("Security", categories);
        Assert.Contains("New Planets", categories);
    }
}
