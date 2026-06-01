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
}
