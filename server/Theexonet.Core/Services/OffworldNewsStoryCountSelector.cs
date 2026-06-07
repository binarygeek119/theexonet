using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services;

/// <summary>Date-seeded fuzzy story count for daily Offworld News editions.</summary>
public static class OffworldNewsStoryCountSelector
{
    private const int SelectorSalt = 0x0FF0;

    public static int ResolveStoryCount(DateOnly editionDate, OffworldNewsOptions options)
    {
        var maxCount = Math.Max(1, options.MaxStoriesPerDay);
        var target = Math.Clamp(options.StoriesPerDay, 1, maxCount);
        var variance = Math.Max(0, options.StoriesPerDayVariance);
        var min = Math.Clamp(options.MinStoriesPerDay, 1, maxCount);

        var rng = CreateRandom(editionDate);
        var delta = (int)Math.Round((rng.NextDouble() + rng.NextDouble() - 1.0) * variance);
        return Math.Clamp(target + delta, min, maxCount);
    }

    private static Random CreateRandom(DateOnly editionDate) =>
        new(HashCode.Combine(editionDate.DayNumber, SelectorSalt));
}
