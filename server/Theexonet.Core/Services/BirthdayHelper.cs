using System.Globalization;

namespace Theexonet.Core.Services;

public static class BirthdayHelper
{
    public static int ComputeAge(DateOnly birthday, DateOnly today)
    {
        var age = today.Year - birthday.Year;
        if (today < birthday.AddYears(age))
        {
            age--;
        }

        return age;
    }

    public static string? TryFormatPublicBirthday(DateOnly? birthday, bool isPublic) =>
        isPublic && birthday.HasValue
            ? birthday.Value.ToString("MMMM d", CultureInfo.InvariantCulture)
            : null;

    public static int? TryComputePublicAge(DateOnly? birthday, bool isPublic, DateOnly today) =>
        isPublic && birthday.HasValue
            ? ComputeAge(birthday.Value, today)
            : null;

    public static bool IsBirthdayToday(DateOnly birthday, DateOnly today)
    {
        if (birthday.Month == today.Month && birthday.Day == today.Day)
        {
            return true;
        }

        if (birthday is { Month: 2, Day: 29 }
            && today is { Month: 2, Day: 28 }
            && !DateTime.IsLeapYear(today.Year))
        {
            return true;
        }

        return false;
    }
}
