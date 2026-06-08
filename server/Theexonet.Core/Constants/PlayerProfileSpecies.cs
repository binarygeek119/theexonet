namespace Theexonet.Core.Constants;

public sealed record PlayerSpeciesOptionDto(string Slug, string Label);

/// <summary>Player profile species — human or a known alien type in the theexonet setting.</summary>
public static class PlayerProfileSpecies
{
    public const string Human = "human";

    public static readonly IReadOnlyList<PlayerSpeciesOptionDto> Catalog =
    [
        new(Human, "Human"),
        new("europan", "Europan"),
        new("callistan", "Callistan"),
        new("martian", "Martian"),
        new("venusian", "Venusian"),
        new("titanian", "Titanian"),
        new("jovian", "Jovian"),
        new("lunar", "Lunar"),
        new("mercurian", "Mercurian"),
        new("saturnian", "Saturnian"),
        new("plutonian", "Plutonian"),
        new("ganymedian", "Ganymedian"),
        new("ceresian", "Ceresian"),
    ];

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

        var match = Catalog.FirstOrDefault(option =>
            string.Equals(option.Slug, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Label, trimmed, StringComparison.OrdinalIgnoreCase));

        return match?.Slug ?? trimmed.ToLowerInvariant();
    }

    public static string? Validate(string? species)
    {
        var normalized = Normalize(species);
        if (normalized.Equals(Human, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var known = Catalog.Any(option =>
            string.Equals(option.Slug, normalized, StringComparison.OrdinalIgnoreCase));
        return known
            ? null
            : "Choose human or a species from the catalog.";
    }

    public static string DisplayLabel(string? species)
    {
        var normalized = Normalize(species);
        if (normalized.Equals(Human, StringComparison.OrdinalIgnoreCase))
        {
            return "Human";
        }

        return Catalog.FirstOrDefault(option =>
                   string.Equals(option.Slug, normalized, StringComparison.OrdinalIgnoreCase))
               ?.Label
               ?? normalized;
    }
}
