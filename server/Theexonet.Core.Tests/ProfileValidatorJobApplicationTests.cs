using Theexonet.Core.Validation;

namespace Theexonet.Core.Tests;

public class ProfileValidatorJobApplicationTests
{
    [Fact]
    public void ValidateJobApplication_RequiresMoodAboutInterests()
    {
        Assert.NotNull(ProfileValidator.ValidateJobApplication("", "bio", "rocks", "", "", "", "", "", ""));
        Assert.NotNull(ProfileValidator.ValidateJobApplication("Ready", "", "rocks", "", "", "", "", "", ""));
        Assert.NotNull(ProfileValidator.ValidateJobApplication("Ready", "bio", "", "", "", "", "", "", ""));
    }

    [Fact]
    public void ValidateJobApplication_AcceptsRequiredFields()
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
}
