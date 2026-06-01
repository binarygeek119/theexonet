using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsTemplateGeneratorTests
{
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
}
