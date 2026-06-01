using Rava.Core.Constants;

namespace Rava.Core.Configuration;

public static class GameCreditsCsvLoader
{
    public static GameCreditsValues LoadFromFile(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path)) : CreateDefault();

    public static GameCreditsValues Parse(string csvContent)
    {
        var values = CreateDefault();
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return values;
        }

        var skipHeader = true;
        foreach (var rawLine in csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 2)
            {
                continue;
            }

            if (skipHeader && IsHeaderRow(columns))
            {
                skipHeader = false;
                continue;
            }

            skipHeader = false;

            if (!decimal.TryParse(columns[1].Trim(), out var amount))
            {
                continue;
            }

            switch (columns[0].Trim())
            {
                case "SignUp":
                    values = values with { SignUp = amount };
                    break;
                case "BirthdayBonus":
                    values = values with { BirthdayBonus = amount };
                    break;
                case "CompanyNameReclaimFee":
                    values = values with { CompanyNameReclaimFee = amount };
                    break;
            }
        }

        return values;
    }

    public static void SaveToFile(string path, GameCreditsValues values)
    {
        var lines = new[]
        {
            "Setting,Amount,Description",
            "# Credit grants and fees. Opens in Excel or Google Sheets.",
            $"SignUp,{FormatAmount(values.SignUp)},Credits granted when a new player registers",
            $"BirthdayBonus,{FormatAmount(values.BirthdayBonus)},Bonus credits on the player's birthday once per year",
            $"CompanyNameReclaimFee,{FormatAmount(values.CompanyNameReclaimFee)},Fee to reclaim a company name within the 30-day limbo window",
        };

        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public static GameCreditsValues CreateDefault() =>
        new()
        {
            SignUp = GameCredits.SignUp,
            BirthdayBonus = GameCredits.BirthdayBonus,
            CompanyNameReclaimFee = GameCredits.CompanyNameReclaimFee,
        };

    private static string FormatAmount(decimal amount) =>
        amount % 1m == 0m ? amount.ToString("0") : amount.ToString("0.##");

    private static bool IsHeaderRow(IReadOnlyList<string> columns) =>
        columns[0].Equals("Setting", StringComparison.OrdinalIgnoreCase);

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

public sealed record GameCreditsValues
{
    public decimal SignUp { get; init; } = GameCredits.SignUp;
    public decimal BirthdayBonus { get; init; } = GameCredits.BirthdayBonus;
    public decimal CompanyNameReclaimFee { get; init; } = GameCredits.CompanyNameReclaimFee;
}
