using Theexonet.Core.Constants;

namespace Theexonet.Core.Validation;

public static class ProfileValidator
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "classic",
        "neon",
        "midnight",
        "solar"
    };

    public const int MaxMoodLength = 120;
    public const int MaxAboutLength = 2000;
    public const int MaxMusicLength = 120;
    public const int MaxInterestsLength = 500;
    public const int MaxSocialLength = 120;

    public static string? ValidateJobApplication(
        string mood,
        string aboutMe,
        string interests,
        string music,
        string discord,
        string bluesky,
        string twitter,
        string youtube,
        string facebook,
        string? profileSpecies = null)
    {
        var speciesError = PlayerProfileSpecies.Validate(profileSpecies);
        if (speciesError is not null)
        {
            return speciesError;
        }

        return ValidateUpdate(mood, aboutMe, music, interests, discord, bluesky, twitter, youtube, facebook);
    }

    public static string? ValidateUpdate(
        string mood,
        string aboutMe,
        string music,
        string interests,
        string discord,
        string bluesky,
        string twitter,
        string youtube,
        string facebook)
    {
        if (mood.Length > MaxMoodLength)
        {
            return $"Mood must be {MaxMoodLength} characters or fewer.";
        }

        if (aboutMe.Length > MaxAboutLength)
        {
            return $"About Me must be {MaxAboutLength} characters or fewer.";
        }

        if (music.Length > MaxMusicLength)
        {
            return $"Now Playing must be {MaxMusicLength} characters or fewer.";
        }

        if (interests.Length > MaxInterestsLength)
        {
            return $"Interests must be {MaxInterestsLength} characters or fewer.";
        }

        foreach (var (label, value) in new (string Label, string Value)[]
                 {
                     ("Discord", discord),
                     ("Bluesky", bluesky),
                     ("Twitter", twitter),
                     ("YouTube", youtube),
                     ("Facebook", facebook)
                 })
        {
            if (value.Length > MaxSocialLength)
            {
                return $"{label} must be {MaxSocialLength} characters or fewer.";
            }
        }

        return null;
    }

    public static string NormalizeTheme(string theme) =>
        AllowedThemes.Contains(theme.Trim()) ? theme.Trim().ToLowerInvariant() : "classic";

    public static string? ValidateAvatarPreset(string? preset)
    {
        if (preset is null || string.IsNullOrWhiteSpace(preset))
        {
            return null;
        }

        return ProfileAvatarPresets.All.Contains(preset.Trim(), StringComparer.OrdinalIgnoreCase)
            ? null
            : "Profile photo style must be male, female, or neutral.";
    }

    public static string? ValidateGenderAndPronouns(string? gender, string? preferredPronouns)
    {
        if (!ProfileGender.IsValid(gender))
        {
            return "Gender must be male, female, trans-female, trans-male, non-binary, or prefer not to say.";
        }

        if (!ProfilePreferredPronouns.IsValid(preferredPronouns))
        {
            return "Preferred pronouns must be he/him, she/her, or they/them.";
        }

        var normalizedGender = ProfileGender.Normalize(gender);
        var normalizedPreferred = ProfilePreferredPronouns.Normalize(preferredPronouns);

        if (ProfileGender.RequiresPreferredPronouns(normalizedGender) &&
            string.IsNullOrEmpty(normalizedPreferred))
        {
            return "Choose preferred pronouns for this gender selection.";
        }

        return null;
    }
}
