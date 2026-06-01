namespace Rava.Core.Constants;

/// <summary>Explicit pronoun choice when gender is non-binary or prefer not to say.</summary>
public static class ProfilePreferredPronouns
{
    public const string HeHim = "he-him";
    public const string SheHer = "she-her";
    public const string TheyThem = "they-them";

    public static IReadOnlyList<string> All { get; } = [HeHim, SheHer, TheyThem];

    public static string Normalize(string? pronouns) =>
        pronouns?.Trim().ToLowerInvariant() switch
        {
            HeHim or "he/him" or "he him" => HeHim,
            SheHer or "she/her" or "she her" => SheHer,
            TheyThem or "they/them" or "they them" => TheyThem,
            "" or null => string.Empty,
            _ => string.Empty,
        };

    public static bool IsValid(string? pronouns) =>
        string.IsNullOrEmpty(Normalize(pronouns)) || All.Contains(Normalize(pronouns), StringComparer.Ordinal);
}
