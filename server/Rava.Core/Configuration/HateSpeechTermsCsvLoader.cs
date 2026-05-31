namespace Rava.Core.Configuration;

public static class HateSpeechTermsCsvLoader
{
    public static string[] LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return Parse(File.ReadAllText(path));
    }

    public static string[] Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return [];
        }

        var terms = new List<string>();
        var skipHeader = true;

        foreach (var rawLine in csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var term = ExtractFirstColumn(line);
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (skipHeader && IsHeaderTerm(term))
            {
                skipHeader = false;
                continue;
            }

            skipHeader = false;
            terms.Add(term.Trim());
        }

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ExtractFirstColumn(string line)
    {
        if (!line.Contains('"'))
        {
            var commaIndex = line.IndexOf(',');
            return commaIndex >= 0 ? line[..commaIndex].Trim() : line.Trim();
        }

        if (line.StartsWith('"'))
        {
            var closingQuote = line.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                return line[1..closingQuote];
            }
        }

        return line.Trim().Trim('"');
    }

    private static bool IsHeaderTerm(string term) =>
        term.Equals("term", StringComparison.OrdinalIgnoreCase)
        || term.Equals("terms", StringComparison.OrdinalIgnoreCase)
        || term.Equals("word", StringComparison.OrdinalIgnoreCase)
        || term.Equals("words", StringComparison.OrdinalIgnoreCase);
}
