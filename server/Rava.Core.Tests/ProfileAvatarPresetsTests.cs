using Rava.Core.Constants;

namespace Rava.Core.Tests;

public class ProfileAvatarPresetsTests
{
    [Theory]
    [InlineData("male", "/images/profile-defaults/male.svg")]
    [InlineData("female", "/images/profile-defaults/female.svg")]
    [InlineData("neutral", "/images/profile-defaults/neutral.svg")]
  public void ResolveDisplayUrl_uses_preset_when_no_custom_upload(string preset, string expectedPath)
    {
        var url = ProfileAvatarPresets.ResolveDisplayUrl(string.Empty, 0, preset);
        Assert.Equal(expectedPath, url);
    }

    [Fact]
    public void ResolveDisplayUrl_uses_custom_upload_when_present()
    {
        var url = ProfileAvatarPresets.ResolveDisplayUrl("/images/avatars/player.png", 3, "female");
        Assert.Equal("/images/avatars/player.png?v=3", url);
    }

    [Theory]
    [InlineData(ProfileGender.Male, ProfileAvatarPresets.Male)]
    [InlineData(ProfileGender.TransMale, ProfileAvatarPresets.Male)]
    [InlineData(ProfileGender.Female, ProfileAvatarPresets.Female)]
    [InlineData(ProfileGender.TransFemale, ProfileAvatarPresets.Female)]
    [InlineData(ProfileGender.NonBinary, ProfileAvatarPresets.Neutral)]
    [InlineData(ProfileGender.PreferNotToSay, ProfileAvatarPresets.Neutral)]
    [InlineData("", ProfileAvatarPresets.Neutral)]
    public void FromGender_maps_to_expected_preset(string gender, string expectedPreset)
    {
        Assert.Equal(expectedPreset, ProfileAvatarPresets.FromGender(gender));
    }
}
