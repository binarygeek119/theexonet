using Rava.Core.Configuration;

namespace Rava.Core.Services;

/// <summary>Date-seeded fuzzy story count for daily Offworld News editions (up to 10).</summary>
public static class OffworldNewsStoryCountSelector
{
    private const int SelectorSalt = 0x0FF0;

    public static int ResolveStoryCount(DateOnly editionDate, OffworldNewsOptions options)
    {
        var max = Math.Clamp(options.MaxStoriesPerDay, 1, 10);
        var target = Math.Clamp(options.StoriesPerDay, 1, max);
        var variance = Math.Max(0, options.StoriesPerDayVariance);
        var min = Math.Clamp(options.MinStoriesPerDay, 1, max);
        var maxCount = Math.Clamp(options.MaxStoriesPerDay, min, 10);

        var rng = CreateRandom(editionDate);
        var delta = (int)Math.Round((rng.NextDouble() + rng.NextDouble() - 1.0) * variance);
        return Math.Clamp(target + delta, min, maxCount);
    }

    private static Random CreateRandom(DateOnly editionDate) =>
        new(HashCode.Combine(editionDate.DayNumber, SelectorSalt));
}
