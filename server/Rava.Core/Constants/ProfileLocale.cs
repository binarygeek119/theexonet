namespace Rava.Core.Constants;

/// <summary>Player UI language (BCP 47 base codes supported by game locales).</summary>
public static class ProfileLocale
{
    public const string Default = "en";

    public static IReadOnlyList<string> All { get; } = ["en", "es", "fr", "de", "pt"];

    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return string.Empty;
        }

        var baseCode = locale.Trim().ToLowerInvariant().Split('-')[0];
        return All.Contains(baseCode, StringComparer.Ordinal) ? baseCode : string.Empty;
    }

    public static bool IsValid(string? locale)
    {
        var normalized = Normalize(locale);
        return !string.IsNullOrEmpty(normalized);
    }
}
