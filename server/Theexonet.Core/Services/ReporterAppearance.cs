namespace Theexonet.Core.Services;

public sealed record ReporterAppearance(
    string Hair = "",
    string Eyes = "",
    string Race = "",
    string Build = "",
    string FacialHair = "",
    string Makeup = "",
    string DistinctiveFeatures = "",
    string Species = ReporterSpecies.Human)
{
    public static ReporterAppearance Empty { get; } = new();

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Hair)
        && string.IsNullOrWhiteSpace(Eyes)
        && string.IsNullOrWhiteSpace(Race)
        && string.IsNullOrWhiteSpace(Build)
        && string.IsNullOrWhiteSpace(FacialHair)
        && string.IsNullOrWhiteSpace(Makeup)
        && string.IsNullOrWhiteSpace(DistinctiveFeatures)
        && ReporterSpecies.IsHuman(Species);

    public bool IsAlien => !ReporterSpecies.IsHuman(Species);

    public string DescribeForPortraitPrompt()
    {
        var parts = new List<string>();
        var speciesLabel = ReporterSpecies.DisplayLabel(Species);
        if (!ReporterSpecies.IsHuman(speciesLabel))
        {
            parts.Add($"species: {speciesLabel} (non-human alien correspondent)");
        }

        Append(parts, "race / skin", Race);
        Append(parts, "hair or cranial crest", Hair);
        Append(parts, "eyes", Eyes);
        Append(parts, "build", Build);
        Append(parts, "facial hair", FacialHair);
        Append(parts, "makeup", Makeup);
        Append(parts, "distinctive alien or facial features", DistinctiveFeatures);
        return parts.Count == 0 ? string.Empty : string.Join("; ", parts);
    }

    private static void Append(List<string> parts, string label, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        parts.Add($"{label}: {trimmed}");
    }
}
