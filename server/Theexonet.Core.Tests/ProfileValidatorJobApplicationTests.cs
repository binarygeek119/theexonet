using Theexonet.Core.Validation;

namespace Theexonet.Core.Tests;

public class ProfileValidatorJobApplicationTests
{
    [Fact]
    public void ValidateJobApplication_AcceptsEmptyProfileFields()
    {
        Assert.Null(ProfileValidator.ValidateJobApplication("", "", "", "", "", "", "", "", ""));
    }

    [Fact]
    public void ValidateJobApplication_AcceptsFilledProfileFields()
    {
        Assert.Null(ProfileValidator.ValidateJobApplication(
            "Ready to mine.",
            "Belt-born operator.",
            "Asteroids and jazz",
            "",
            "",
            "",
            "",
            "",
            ""));
    }

    [Fact]
    public void ValidateJobApplication_RejectsInvalidSpecies()
    {
        Assert.NotNull(ProfileValidator.ValidateJobApplication(
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "xenomorph"));
    }
}
