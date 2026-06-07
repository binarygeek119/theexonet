using Theexonet.Core.Services;

namespace Theexonet.Core.Configuration;

public static class OffworldNewsReportersCsvLoader
{
    public const string Header =
        "Slug,DisplayName,Title,Beat,Bureau,Personality,WritingVoice,DirectoryBio,OnnBio,StoryKicker,Specialties,Gender,NotableLocations,NotableStories,Hair,Eyes,Race,Build,FacialHair,Makeup,DistinctiveFeatures,Species";

    public const string HeaderComment =
        "# ONN reporter personalities. Specialties, NotableLocations, NotableStories: separate with semicolons (;). Gender: male or female (portrait AI). Race and Species drive portrait AI; Species: Human or alien type (Europan, Callistan, etc.).";

    public static IReadOnlyList<OffworldNewsReporterProfile> LoadFromFile(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : [];

    public static IReadOnlyList<OffworldNewsReporterProfile> Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return [];
        }

        var reporters = new List<OffworldNewsReporterProfile>();
        var skipHeader = true;

        foreach (var rawLine in csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 11)
            {
                continue;
            }

            if (skipHeader && IsHeaderRow(columns))
            {
                skipHeader = false;
                continue;
            }

            skipHeader = false;

            var slug = columns[0].Trim();
            if (slug.Length == 0)
            {
                continue;
            }

            var gender = columns.Count >= 12
                ? OffworldNewsReporterPortraitGender.Normalize(columns[11])
                : string.Empty;
            if (gender.Length == 0)
            {
                gender = OffworldNewsReporterPortraitGender.InferForSlug(slug);
            }

            reporters.Add(new OffworldNewsReporterProfile(
                slug,
                columns[1].Trim(),
                columns[2].Trim(),
                columns[3].Trim(),
                columns[4].Trim(),
                columns[5].Trim(),
                columns[6].Trim(),
                columns[7].Trim(),
                columns[8].Trim(),
                columns[9].Trim(),
                ParseList(columns, 10),
                gender,
                ParseList(columns, 12),
                ParseList(columns, 13),
                new ReporterAppearance(
                    Column(columns, 14),
                    Column(columns, 15),
                    Column(columns, 16),
                    Column(columns, 17),
                    Column(columns, 18),
                    Column(columns, 19),
                    Column(columns, 20),
                    NormalizeSpeciesColumn(Column(columns, 21)))));
        }

        return reporters;
    }

    public static void SaveToFile(string path, IReadOnlyList<OffworldNewsReporterProfile> reporters)
    {
        var lines = new List<string> { Header, HeaderComment };

        foreach (var reporter in reporters)
        {
            lines.Add(string.Join(
                ',',
                EscapeCsvField(reporter.Slug),
                EscapeCsvField(reporter.DisplayName),
                EscapeCsvField(reporter.Title),
                EscapeCsvField(reporter.Beat),
                EscapeCsvField(reporter.Bureau),
                EscapeCsvField(reporter.Personality),
                EscapeCsvField(reporter.WritingVoice),
                EscapeCsvField(reporter.DirectoryBio),
                EscapeCsvField(reporter.OnnBio),
                EscapeCsvField(reporter.StoryKicker),
                EscapeCsvField(string.Join(';', reporter.Specialties)),
                EscapeCsvField(reporter.Gender),
                EscapeCsvField(string.Join(';', reporter.NotableLocations)),
                EscapeCsvField(string.Join(';', reporter.NotableStories)),
                EscapeCsvField(reporter.Appearance.Hair),
                EscapeCsvField(reporter.Appearance.Eyes),
                EscapeCsvField(reporter.Appearance.Race),
                EscapeCsvField(reporter.Appearance.Build),
                EscapeCsvField(reporter.Appearance.FacialHair),
                EscapeCsvField(reporter.Appearance.Makeup),
                EscapeCsvField(reporter.Appearance.DistinctiveFeatures),
                EscapeCsvField(ReporterSpecies.Normalize(reporter.Appearance.Species))));
        }

        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public static bool HasExtendedColumns(string path) =>
        HasSpeciesColumn(path) || HasLegacyExtendedColumns(path);

    public static bool HasSpeciesColumn(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            return columns.Count >= 22
                || HeaderContainsSpecies(columns);
        }

        return false;
    }

    private static bool HasLegacyExtendedColumns(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            return columns.Count >= 21
                || (columns.Count >= 1 && columns[0].Contains("NotableLocations", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static bool HeaderContainsSpecies(IReadOnlyList<string> columns) =>
        columns.Any(column => column.Contains("Species", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeSpeciesColumn(string value) =>
        string.IsNullOrWhiteSpace(value) ? ReporterSpecies.Human : ReporterSpecies.Normalize(value);

    private static IReadOnlyList<string> ParseList(IReadOnlyList<string> columns, int index) =>
        ParseDelimitedList(Column(columns, index));

    public static IReadOnlyList<string> ParseDelimitedList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(entry => entry.Length > 0)
                .ToList();

    private static string Column(IReadOnlyList<string> columns, int index) =>
        index < columns.Count ? columns[index].Trim() : string.Empty;

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private static bool IsHeaderRow(IReadOnlyList<string> columns) =>
        columns[0].Equals("Slug", StringComparison.OrdinalIgnoreCase);

    private static List<string> SplitCsvLine(string line)
    {
        var columns = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        columns.Add(current.ToString());
        return columns;
    }
}
