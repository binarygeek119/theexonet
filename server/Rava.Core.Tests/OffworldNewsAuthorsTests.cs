using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsAuthorsTests
{
    [Fact]
    public void Reporters_has_fifteen_unique_bylines()
    {
        Assert.Equal(15, OffworldNewsAuthors.Reporters.Count);
        Assert.Equal(OffworldNewsAuthors.Reporters.Count, OffworldNewsAuthors.Reporters.Distinct().Count());
    }

    [Fact]
    public void PickForStory_is_deterministic_per_edition_and_index()
    {
        var date = new DateOnly(2026, 6, 1);
        var first = OffworldNewsAuthors.PickForStory(date, 0);
        var second = OffworldNewsAuthors.PickForStory(date, 0);
        var third = OffworldNewsAuthors.PickForStory(date, 1);

        Assert.Equal(first, second);
        Assert.Contains(first, OffworldNewsAuthors.Reporters);
        Assert.Contains(third, OffworldNewsAuthors.Reporters);
    }
}
