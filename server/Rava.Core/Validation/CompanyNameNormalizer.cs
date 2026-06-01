using System.Text.RegularExpressions;

namespace Rava.Core.Validation;

public static partial class CompanyNameNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string NormalizeDisplay(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return WhitespaceRegex().Replace(trimmed, " ");
    }

    public static string NormalizeKey(string name) =>
        NormalizeDisplay(name).ToLowerInvariant();
}
