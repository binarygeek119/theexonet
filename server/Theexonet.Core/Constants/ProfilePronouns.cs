namespace Theexonet.Core.Constants;

public sealed record ProfilePronounSet(
    string Subject,
    string Object,
    string Possessive,
    string Label);

/// <summary>Resolves subject/object/possessive forms from gender and optional explicit pronouns.</summary>
public static class ProfilePronouns
{
    public static ProfilePronounSet Resolve(string? gender, string? preferredPronouns)
    {
        var normalizedGender = ProfileGender.Normalize(gender);
        var normalizedPreferred = ProfilePreferredPronouns.Normalize(preferredPronouns);

        if (ProfileGender.RequiresPreferredPronouns(normalizedGender))
        {
            return FromPreferred(normalizedPreferred);
        }

        return normalizedGender switch
        {
            ProfileGender.Male or ProfileGender.TransMale => HeHim,
            ProfileGender.Female or ProfileGender.TransFemale => SheHer,
            _ => FromPreferred(normalizedPreferred),
        };
    }

    public static ProfilePronounSet HeHim { get; } = new("he", "him", "his", "he/him");

    public static ProfilePronounSet SheHer { get; } = new("she", "her", "her", "she/her");

    public static ProfilePronounSet TheyThem { get; } = new("they", "them", "their", "they/them");

    private static ProfilePronounSet FromPreferred(string preferred) =>
        preferred switch
        {
            ProfilePreferredPronouns.HeHim => HeHim,
            ProfilePreferredPronouns.SheHer => SheHer,
            ProfilePreferredPronouns.TheyThem => TheyThem,
            _ => TheyThem,
        };
}
