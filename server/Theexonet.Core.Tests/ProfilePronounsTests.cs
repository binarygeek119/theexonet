using Theexonet.Core.Constants;

namespace Theexonet.Core.Tests;

public class ProfilePronounsTests
{
    [Theory]
    [InlineData(ProfileGender.Male, "", "he", "him", "his")]
    [InlineData(ProfileGender.Female, "", "she", "her", "her")]
    [InlineData(ProfileGender.TransMale, "", "he", "him", "his")]
    [InlineData(ProfileGender.TransFemale, "", "she", "her", "her")]
    [InlineData(ProfileGender.NonBinary, ProfilePreferredPronouns.TheyThem, "they", "them", "their")]
    [InlineData(ProfileGender.PreferNotToSay, ProfilePreferredPronouns.HeHim, "he", "him", "his")]
    [InlineData("", "", "they", "them", "their")]
    public void Resolve_UsesGenderOrPreferred(
        string gender,
        string preferred,
        string subject,
        string obj,
        string possessive)
    {
        var set = ProfilePronouns.Resolve(gender, preferred);
        Assert.Equal(subject, set.Subject);
        Assert.Equal(obj, set.Object);
        Assert.Equal(possessive, set.Possessive);
    }

    [Fact]
    public void ProfileGender_RequiresPreferredPronouns_ForNonBinaryAndPreferNotToSay()
    {
        Assert.True(ProfileGender.RequiresPreferredPronouns(ProfileGender.NonBinary));
        Assert.True(ProfileGender.RequiresPreferredPronouns(ProfileGender.PreferNotToSay));
        Assert.False(ProfileGender.RequiresPreferredPronouns(ProfileGender.Male));
    }
}
