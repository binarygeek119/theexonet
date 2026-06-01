namespace Rava.Core.Constants;

/// <summary>Player-reported gender for profile display and pronoun resolution.</summary>
public static class ProfileGender
{
    public const string Male = "male";
    public const string Female = "female";
    public const string TransFemale = "trans-female";
    public const string TransMale = "trans-male";
    public const string NonBinary = "non-binary";
    public const string PreferNotToSay = "prefer-not-to-say";

    public static IReadOnlyList<string> All { get; } =
    [
        Male,
        Female,
        TransFemale,
        TransMale,
        NonBinary,
        PreferNotToSay,
    ];

    public static string Normalize(string? gender) =>
        gender?.Trim().ToLowerInvariant() switch
        {
            Male => Male,
            Female => Female,
            "transfemale" or "trans_female" or TransFemale => TransFemale,
            "transmale" or "trans_male" or TransMale => TransMale,
            "nonbinary" or "non_binary" or NonBinary => NonBinary,
            "prefer-not-to-say" or "prefernottosay" or "prefer_not_to_say" or PreferNotToSay => PreferNotToSay,
            "" or null => string.Empty,
            _ => string.Empty,
        };

    public static bool IsValid(string? gender) =>
        string.IsNullOrEmpty(Normalize(gender)) || All.Contains(Normalize(gender), StringComparer.Ordinal);

    public static bool RequiresPreferredPronouns(string? gender) =>
        Normalize(gender) is NonBinary or PreferNotToSay;
}
