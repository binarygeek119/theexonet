using Rava.Core.Services;

namespace Rava.Core.Configuration;

public static class OffworldNewsReportersCsvLoader
{
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

            var specialties = columns[10]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(entry => entry.Length > 0)
                .ToList();

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
                specialties));
        }

        return reporters;
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
