namespace Rava.Core.Services;

public static class UtcGameClock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static DateTime NextDayBoundaryUtc =>
        Today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static TimeSpan TimeUntilNextDay => NextDayBoundaryUtc - DateTime.UtcNow;
}
