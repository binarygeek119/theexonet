using System.Text.RegularExpressions;
using Rava.Core.Constants;

namespace Rava.Core.Validation;

public static partial class CompanyNameValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 .&\-']*[A-Za-z0-9.]$|^[A-Za-z0-9]$")]
    private static partial Regex CompanyNamePattern();

    public static string? Validate(string? name)
    {
        var display = CompanyNameNormalizer.NormalizeDisplay(name ?? string.Empty);
        if (display.Length < CompanyNameFormats.MinLength)
        {
            return $"Company name must be at least {CompanyNameFormats.MinLength} characters.";
        }

        if (display.Length > CompanyNameFormats.MaxLength)
        {
            return $"Company name must be {CompanyNameFormats.MaxLength} characters or fewer.";
        }

        if (!CompanyNamePattern().IsMatch(display))
        {
            return CompanyNameFormats.ValidationMessage;
        }

        if (!display.Any(char.IsLetter))
        {
            return "Company name must include at least one letter.";
        }

        return null;
    }
}
