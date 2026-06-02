namespace Rava.Core.Services;

/// <summary>Portrait gender for AI-generated ONN reporter art (male / female).</summary>
public static class OffworldNewsReporterPortraitGender
{
    public const string Male = "male";
    public const string Female = "female";

    public static IReadOnlyList<string> All { get; } = [Male, Female];

    public static string Normalize(string? gender) =>
        gender?.Trim().ToLowerInvariant() switch
        {
            Male => Male,
            Female => Female,
            "m" or "man" => Male,
            "f" or "woman" => Female,
            _ => string.Empty,
        };

    public static string InferForSlug(string slug)
    {
        if (KnownBySlug.TryGetValue(slug.Trim().ToLowerInvariant(), out var known))
        {
            return known;
        }

        return SlugHashIsEven(slug) ? Female : Male;
    }

    public static string PortraitSubjectPhrase(string? gender, string? species = null)
    {
        if (ReporterSpecies.IsHuman(species))
        {
            return Normalize(gender) switch
            {
                Female => "clearly female adult human woman journalist",
                Male => "clearly male adult human man journalist",
                _ => "adult human journalist",
            };
        }

        var speciesLabel = ReporterSpecies.DisplayLabel(species);
        return Normalize(gender) switch
        {
            Female =>
                $"clearly female adult {speciesLabel} alien journalist with believable non-human anatomy",
            Male =>
                $"clearly male adult {speciesLabel} alien journalist with believable non-human anatomy",
            _ =>
                $"adult {speciesLabel} alien journalist with believable non-human anatomy",
        };
    }

    private static bool SlugHashIsEven(string slug)
    {
        var hash = 0;
        foreach (var ch in slug)
        {
            hash = (hash * 31) + ch;
        }

        return (hash & 1) == 0;
    }

    private static readonly Dictionary<string, string> KnownBySlug =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mira-solano"] = Female,
            ["jonah-kest"] = Male,
            ["priya-menon"] = Female,
            ["cassian-holt"] = Male,
            ["elena-varga"] = Female,
            ["marcus-whitaker"] = Male,
            ["sable-nguyen"] = Female,
            ["theo-brassard"] = Male,
            ["ingrid-falk"] = Female,
            ["devon-ashcroft"] = Male,
            ["lena-okonkwo"] = Female,
            ["rafael-cruz"] = Male,
            ["yumiko-ito"] = Female,
            ["anders-lindqvist"] = Male,
            ["zara-pemberton"] = Female,
        };
}
