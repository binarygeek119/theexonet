using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class OffworldNewsEditionStoryTypesTests
{
    [Fact]
    public void TypesForEdition_includes_all_core_types_when_story_count_is_five()
    {
        var types = OffworldNewsEditionStoryTypes.TypesForEdition(new DateOnly(2026, 6, 7), 5);

        Assert.Equal(5, types.Count);
        Assert.Equal(OffworldNewsEditionStoryTypes.Core, types);
    }

    [Fact]
    public void TypesForEdition_prioritizes_core_types_when_story_count_is_below_five()
    {
        var types = OffworldNewsEditionStoryTypes.TypesForEdition(new DateOnly(2026, 6, 7), 3);

        Assert.Equal(3, types.Count);
        Assert.Equal(["Politics", "New Planets", "Work"], types);
    }

    [Theory]
    [InlineData("Markets", "Stocks")]
    [InlineData("Frontier", "New Planets")]
    [InlineData("work stuff", "Work")]
    [InlineData("companys", "Companies")]
    public void Normalize_maps_legacy_and_informal_labels(string input, string expected) =>
        Assert.Equal(expected, OffworldNewsEditionStoryTypes.Normalize(input));
}
