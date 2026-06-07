using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services;

/// <summary>Date-seeded fuzzy daily inmate intake count for Foreverfall Penitentiary.</summary>
public static class ForeverfallIntakeSelector
{
    private const int SelectorSalt = 0xFF11;

    public static int ResolveIntakeCount(DateOnly intakeDate, ForeverfallOptions options)
    {
        var maxCount = Math.Max(1, options.MaxDailyIntake);
        var target = Math.Clamp(options.TargetDailyIntake, 1, maxCount);
        var variance = Math.Max(0, options.IntakeVariance);
        var min = Math.Clamp(options.MinDailyIntake, 1, maxCount);

        var rng = CreateRandom(intakeDate);
        var delta = (int)Math.Round((rng.NextDouble() + rng.NextDouble() - 1.0) * variance);
        return Math.Clamp(target + delta, min, maxCount);
    }

    public static (int MaleCount, int FemaleCount) SplitByGender(int total, DateOnly intakeDate)
    {
        total = Math.Max(0, total);
        var male = total / 2;
        if (total % 2 == 1)
        {
            var rng = CreateRandom(intakeDate, salt: 0x12);
            if (rng.NextDouble() >= 0.5)
            {
                male++;
            }
        }

        return (male, total - male);
    }

    private static Random CreateRandom(DateOnly intakeDate, int salt = 0) =>
        new(HashCode.Combine(intakeDate.DayNumber, salt, SelectorSalt));
}
