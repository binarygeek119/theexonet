using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsStoryCountSelectorTests
{
    private static readonly OffworldNewsOptions DefaultOptions = new()
    {
        StoriesPerDay = 5,
        StoriesPerDayVariance = 3,
        MinStoriesPerDay = 1,
        MaxStoriesPerDay = 10,
    };

    [Fact]
    public void ResolveStoryCount_stays_within_fuzzy_band()
    {
        for (var day = 0; day < 120; day++)
        {
            var date = new DateOnly(2026, 1, 1).AddDays(day);
            var count = OffworldNewsStoryCountSelector.ResolveStoryCount(date, DefaultOptions);
            Assert.InRange(count, 2, 8);
        }
    }

    [Fact]
    public void ResolveStoryCount_never_exceeds_max_of_ten()
    {
        var highTarget = new OffworldNewsOptions
        {
            StoriesPerDay = 10,
            StoriesPerDayVariance = 3,
            MinStoriesPerDay = 1,
            MaxStoriesPerDay = 10,
        };

        for (var day = 0; day < 90; day++)
        {
            var date = new DateOnly(2026, 3, 1).AddDays(day);
            var count = OffworldNewsStoryCountSelector.ResolveStoryCount(date, highTarget);
            Assert.InRange(count, 7, 10);
        }
    }

    [Fact]
    public void ResolveStoryCount_is_stable_for_same_date()
    {
        var date = new DateOnly(2026, 5, 29);
        var first = OffworldNewsStoryCountSelector.ResolveStoryCount(date, DefaultOptions);
        var second = OffworldNewsStoryCountSelector.ResolveStoryCount(date, DefaultOptions);
        Assert.Equal(first, second);
    }
}
