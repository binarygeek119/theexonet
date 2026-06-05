using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class ForeverfallIntakeSelectorTests
{
    [Fact]
    public void ResolveIntakeCount_IsDeterministicForSameDate()
    {
        var options = new ForeverfallOptions
        {
            TargetDailyIntake = 15,
            IntakeVariance = 8,
            MinDailyIntake = 7,
            MaxDailyIntake = 23,
        };
        var date = new DateOnly(2026, 6, 1);

        var first = ForeverfallIntakeSelector.ResolveIntakeCount(date, options);
        var second = ForeverfallIntakeSelector.ResolveIntakeCount(date, options);

        Assert.Equal(first, second);
        Assert.InRange(first, options.MinDailyIntake, options.MaxDailyIntake);
    }

    [Fact]
    public void ResolveIntakeCount_ClampsToMaxDailyIntake()
    {
        var options = new ForeverfallOptions
        {
            TargetDailyIntake = 100,
            IntakeVariance = 50,
            MinDailyIntake = 1,
            MaxDailyIntake = 23,
        };

        var count = ForeverfallIntakeSelector.ResolveIntakeCount(new DateOnly(2026, 1, 15), options);

        Assert.InRange(count, 1, 23);
    }

    [Fact]
    public void SplitByGender_SumsToTotal()
    {
        var (male, female) = ForeverfallIntakeSelector.SplitByGender(15, new DateOnly(2026, 6, 1));

        Assert.Equal(15, male + female);
        Assert.InRange(male, 7, 8);
        Assert.InRange(female, 7, 8);
    }
}
