namespace Rava.Core.Services;

public static class BirthdayHelper
{
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
