using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Rava.Core.Validation;

public static partial class AuthValidator
{
    private static readonly Regex UsernamePattern = UsernameRegex();
    private const int MinimumAgeYears = 13;
    private const int MaximumAgeYears = 120;

    public static string? ValidateRegistration(string username, string email, string password, string birthday)
    {
        var usernameError = ValidateUsername(username);
        if (usernameError is not null)
        {
            return usernameError;
        }

        var emailError = ValidateEmail(email);
        if (emailError is not null)
        {
            return emailError;
        }

        var passwordError = ValidatePassword(password);
        if (passwordError is not null)
        {
            return passwordError;
        }

        return ValidateBirthday(birthday);
    }

    public static string? ValidateBirthday(string birthday)
    {
        if (string.IsNullOrWhiteSpace(birthday))
        {
            return "Birthday is required.";
        }

        if (!DateOnly.TryParseExact(
                birthday.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return "Enter a valid birthday (YYYY-MM-DD).";
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (parsed > today)
        {
            return "Birthday cannot be in the future.";
        }

        var minBirthday = today.AddYears(-MaximumAgeYears);
        if (parsed < minBirthday)
        {
            return "Enter a valid birthday.";
        }

        var youngestAllowed = today.AddYears(-MinimumAgeYears);
        if (parsed > youngestAllowed)
        {
            return $"You must be at least {MinimumAgeYears} years old to sign up.";
        }

        return null;
    }

    public static string? ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username is required.";
        }

        if (username.Length < 3 || username.Length > 32)
        {
            return "Username must be 3–32 characters.";
        }

        if (!UsernamePattern.IsMatch(username))
        {
            return "Username may only contain letters, numbers, and underscores.";
        }

        return null;
    }

    public static string? ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required.";
        }

        email = email.Trim();
        if (email.Length > 254)
        {
            return "Email is too long.";
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return "Enter a valid email address.";
        }

        return null;
    }

    public static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < 8)
        {
            return "Password must be at least 8 characters.";
        }

        return null;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-zA-Z0-9_]+$")]
    private static partial Regex UsernameRegex();
}
