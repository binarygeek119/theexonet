namespace Rava.Core.Constants;

public static class BanLevels
{
    public const string FifteenMinutes = "15mins";
    public const string SixHours = "6hours";
    public const string TwelveHours = "12hours";
    public const string OneDay = "1day";
    public const string OneWeek = "1week";
    public const string TwoWeeks = "2weeks";
    public const string ThreeWeeks = "3weeks";
    public const string OneMonth = "1month";
    public const string TwoMonths = "2months";
    public const string SixMonths = "6months";
    public const string OneYear = "1year";
    public const string TwoYears = "2years";
    public const string ThreeYears = "3years";
    public const string Life = "life";

    private static readonly (string Code, string Label)[] AllLevels =
    [
        (FifteenMinutes, "15 minutes"),
        (SixHours, "6 hours"),
        (TwelveHours, "12 hours"),
        (OneDay, "1 day"),
        (OneWeek, "1 week"),
        (TwoWeeks, "2 weeks"),
        (ThreeWeeks, "3 weeks"),
        (OneMonth, "1 month"),
        (TwoMonths, "2 months"),
        (SixMonths, "6 months"),
        (OneYear, "1 year"),
        (TwoYears, "2 years"),
        (ThreeYears, "3 years"),
        (Life, "Life")
    ];

    public static IReadOnlyList<(string Code, string Label)> Options => AllLevels;

    public static bool TryParse(string? value, out string normalized)
    {
        normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var level in AllLevels)
        {
            if (level.Code == normalized)
            {
                return true;
            }
        }

        return false;
    }

    public static string GetLabel(string code) =>
        AllLevels.FirstOrDefault(level => level.Code == code).Label ?? code;

    public static DateTime? CalculateExpiresAt(string code, DateTime fromUtc) =>
        code switch
        {
            FifteenMinutes => fromUtc.AddMinutes(15),
            SixHours => fromUtc.AddHours(6),
            TwelveHours => fromUtc.AddHours(12),
            OneDay => fromUtc.AddDays(1),
            OneWeek => fromUtc.AddDays(7),
            TwoWeeks => fromUtc.AddDays(14),
            ThreeWeeks => fromUtc.AddDays(21),
            OneMonth => fromUtc.AddMonths(1),
            TwoMonths => fromUtc.AddMonths(2),
            SixMonths => fromUtc.AddMonths(6),
            OneYear => fromUtc.AddYears(1),
            TwoYears => fromUtc.AddYears(2),
            ThreeYears => fromUtc.AddYears(3),
            Life => null,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown ban level.")
        };

    public static bool IsPermanent(string code) => code == Life;

    public static string FormatBanMessage(string banLevel, DateTime? expiresAtUtc)
    {
        if (IsPermanent(banLevel))
        {
            return "Your account is permanently banned.";
        }

        if (expiresAtUtc is null)
        {
            return "Your account is banned.";
        }

        return $"Your account is banned until {expiresAtUtc.Value:yyyy-MM-dd HH:mm} UTC.";
    }
}
