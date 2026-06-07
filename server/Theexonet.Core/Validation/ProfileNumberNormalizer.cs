using System.Text.RegularExpressions;
using Theexonet.Core.Constants;

namespace Theexonet.Core.Validation;

public static partial class ProfileNumberNormalizer
{
    [GeneratedRegex(@"^!\(\d{3}\)\d{3}-\d{4}$")]
    private static partial Regex LegacyPattern();

    [GeneratedRegex(@"^![0-9A-Z]{3}-\d{4}-[0-9A-Z]{4}$", RegexOptions.IgnoreCase)]
    private static partial Regex SciFiPattern();

    public static string Example => ProfileNumberFormats.Example;

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (LegacyPattern().IsMatch(trimmed))
        {
            return trimmed;
        }

        var sciFi = trimmed.ToUpperInvariant();
        if (SciFiPattern().IsMatch(sciFi))
        {
            return sciFi;
        }

        var digits = string.Concat(trimmed.Where(char.IsDigit));
        if (digits.Length == 10)
        {
            return $"!({digits[..3]}){digits[3..6]}-{digits[6..]}";
        }

        var body = string.Concat(trimmed.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (body.Length == 11 && body.AsSpan(3, 4).ToString().All(char.IsDigit))
        {
            return $"!{body[..3]}-{body.Substring(3, 4)}-{body[7..]}";
        }

        return null;
    }
}
