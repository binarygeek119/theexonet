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
    public void PlaceholderImageForCategory_includes_frontier_and_security()
    {
        Assert.Contains("frontier", OffworldNewsTemplateGenerator.PlaceholderImageForCategory("Frontier"));
        Assert.Contains("security", OffworldNewsTemplateGenerator.PlaceholderImageForCategory("Security"));
    }

    [Fact]
    public void Generate_can_include_frontier_and_security_story_templates()
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

        Assert.Contains("Frontier", categories);
        Assert.Contains("Security", categories);
    }
}
