namespace Theexonet.Core.Services;

/// <summary>Reporter species for portrait AI (human correspondents or believable aliens).</summary>
public static class ReporterSpecies
{
    public const string Human = "human";

    public static string Normalize(string? species)
    {
        var trimmed = species?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Equals(Human, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("terran", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("earth", StringComparison.OrdinalIgnoreCase))
        {
            return Human;
        }

        return trimmed;
    }

    public static bool IsHuman(string? species) =>
        Normalize(species).Equals(Human, StringComparison.OrdinalIgnoreCase);

    public static string DisplayLabel(string? species)
    {
        var normalized = Normalize(species);
        return IsHuman(normalized)
            ? "human"
            : normalized;
    }
}
