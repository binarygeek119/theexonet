using Rava.Core.Services;

namespace Rava.Core.Tests;

public class BirthdayHelperTests
{
    [Fact]
    public void IsBirthdayToday_MatchesMonthAndDay()
    {
        var birthday = new DateOnly(1990, 5, 29);
        var today = new DateOnly(2026, 5, 29);

        Assert.True(BirthdayHelper.IsBirthdayToday(birthday, today));
    }

    [Fact]
    public void IsBirthdayToday_DoesNotMatchOtherDays()
    {
        var birthday = new DateOnly(1990, 5, 29);
        var today = new DateOnly(2026, 5, 28);

        Assert.False(BirthdayHelper.IsBirthdayToday(birthday, today));
    }

    [Fact]
    public void IsBirthdayToday_Feb29MatchesFeb28InNonLeapYear()
    {
        var birthday = new DateOnly(2000, 2, 29);
        var today = new DateOnly(2025, 2, 28);

        Assert.True(BirthdayHelper.IsBirthdayToday(birthday, today));
    }

    [Fact]
    public void IsBirthdayToday_Feb29MatchesFeb29InLeapYear()
    {
        var birthday = new DateOnly(2000, 2, 29);
        var today = new DateOnly(2024, 2, 29);

        Assert.True(BirthdayHelper.IsBirthdayToday(birthday, today));
    }
}
