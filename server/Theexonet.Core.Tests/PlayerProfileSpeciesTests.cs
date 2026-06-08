using Theexonet.Core.Constants;

namespace Theexonet.Core.Tests;

public class PlayerProfileSpeciesTests
{
    [Theory]
    [InlineData(null, PlayerProfileSpecies.Human)]
    [InlineData("Human", PlayerProfileSpecies.Human)]
    [InlineData("Europan", "europan")]
    public void Normalize_maps_catalog_species(string? input, string expected) =>
        Assert.Equal(expected, PlayerProfileSpecies.Normalize(input));

    [Fact]
    public void Validate_rejects_unknown_species() =>
        Assert.NotNull(PlayerProfileSpecies.Validate("xenomorph"));

    [Fact]
    public void DisplayLabel_human() =>
        Assert.Equal("Human", PlayerProfileSpecies.DisplayLabel("human"));
}
