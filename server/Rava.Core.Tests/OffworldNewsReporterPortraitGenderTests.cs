using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterPortraitGenderTests
{
    [Theory]
    [InlineData("female", OffworldNewsReporterPortraitGender.Female)]
    [InlineData("Male", OffworldNewsReporterPortraitGender.Male)]
    [InlineData("woman", OffworldNewsReporterPortraitGender.Female)]
    [InlineData("man", OffworldNewsReporterPortraitGender.Male)]
    public void Normalize_accepts_common_values(string input, string expected) =>
        Assert.Equal(expected, OffworldNewsReporterPortraitGender.Normalize(input));

    [Fact]
    public void InferForSlug_maps_known_roster()
    {
        Assert.Equal(OffworldNewsReporterPortraitGender.Female, OffworldNewsReporterPortraitGender.InferForSlug("mira-solano"));
        Assert.Equal(OffworldNewsReporterPortraitGender.Male, OffworldNewsReporterPortraitGender.InferForSlug("jonah-kest"));
    }
}
