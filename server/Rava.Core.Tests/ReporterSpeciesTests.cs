using Rava.Core.Services;

namespace Rava.Core.Tests;

public class ReporterSpeciesTests
{
    [Theory]
    [InlineData(null, ReporterSpecies.Human)]
    [InlineData("", ReporterSpecies.Human)]
    [InlineData("human", ReporterSpecies.Human)]
    [InlineData("Terran", ReporterSpecies.Human)]
    [InlineData("Europan", "Europan")]
    [InlineData(" Callistan ", "Callistan")]
    public void Normalize_maps_human_aliases_and_preserves_custom_species(string? input, string expected) =>
        Assert.Equal(expected, ReporterSpecies.Normalize(input));

    [Fact]
    public void IsHuman_detects_non_human_species() =>
        Assert.False(ReporterSpecies.IsHuman("Europan"));
}
