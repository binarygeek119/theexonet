using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsImageAspectCatalogTests
{
    [Fact]
    public void Pick_IsStableForSameStory()
    {
        var date = new DateOnly(2026, 5, 29);

        var first = OffworldNewsImageAspectCatalog.Pick(date, "cargo-backlog", 2);
        var second = OffworldNewsImageAspectCatalog.Pick(date, "cargo-backlog", 2);

        Assert.Equal(first.Key, second.Key);
        Assert.Equal(first.ApiSize, second.ApiSize);
    }

    [Fact]
    public void Pick_VariesAcrossStoriesInSameEdition()
    {
        var date = new DateOnly(2026, 5, 29);
        var aspects = Enumerable.Range(0, 12)
            .Select(index => OffworldNewsImageAspectCatalog.Pick(date, $"story-{index}", index).Key)
            .Distinct()
            .ToList();

        Assert.True(aspects.Count >= 2);
    }

    [Theory]
    [InlineData("landscape", "1792x1024")]
    [InlineData("square", "1024x1024")]
    [InlineData("portrait", "1024x1792")]
    public void ResolveApiSize_ReturnsConfiguredSize(string key, string expectedSize)
    {
        Assert.Equal(expectedSize, OffworldNewsImageAspectCatalog.ResolveApiSize(key));
    }
}
