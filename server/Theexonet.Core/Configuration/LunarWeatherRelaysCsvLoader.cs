using Theexonet.Core.Services;

namespace Theexonet.Core.Configuration;

public static class LunarWeatherRelaysCsvLoader
{
    public const string Header = "Id,Slug,Name,Region,Sector,BodyType";

    public static IReadOnlyList<LunarWeatherRelayProfile> LoadFromFile(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : [];

    public static IReadOnlyList<LunarWeatherRelayProfile> Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return [];
        }

        var relays = new List<LunarWeatherRelayProfile>();
        var skipHeader = true;

        foreach (var rawLine in csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 6)
            {
                continue;
            }

            if (skipHeader && IsHeaderRow(columns))
            {
                skipHeader = false;
                continue;
            }

            skipHeader = false;
            var id = columns[0].Trim();
            if (id.Length == 0)
            {
                continue;
            }

            relays.Add(new LunarWeatherRelayProfile(
                id,
                columns[1].Trim(),
                columns[2].Trim(),
                columns[3].Trim(),
                columns[4].Trim(),
                columns[5].Trim()));
        }

        return relays;
    }

    private static bool IsHeaderRow(IReadOnlyList<string> columns) =>
        columns[0].Equals("Id", StringComparison.OrdinalIgnoreCase);

    private static List<string> SplitCsvLine(string line)
    {
        var columns = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
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
