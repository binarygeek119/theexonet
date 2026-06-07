using Theexonet.Core.Configuration;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class LunarWeatherRelaySelectorTests
{
    private static readonly LunarWeatherOptions DefaultOptions = new()
    {
        RelayPoolSize = 100,
        TargetOperationalCount = 30,
        OperationalVariance = 5,
        MinOperationalCount = 22,
        MaxOperationalCount = 38,
    };

    [Fact]
    public void ResolveOperationalCount_stays_within_fuzzy_band()
    {
        for (var day = 0; day < 120; day++)
        {
            var date = new DateOnly(2026, 1, 1).AddDays(day);
            var count = LunarWeatherRelaySelector.ResolveOperationalCount(date, DefaultOptions);
            Assert.InRange(count, DefaultOptions.MinOperationalCount, DefaultOptions.MaxOperationalCount);
        }
    }

    [Fact]
    public void SelectOperationalAndOutage_partitions_entire_pool()
    {
        var date = new DateOnly(2026, 6, 4);
        var pool = LunarWeatherRelayCatalog.BuildDefaultPool();
        var operationalCount = LunarWeatherRelaySelector.ResolveOperationalCount(date, DefaultOptions);
        var operational = LunarWeatherRelaySelector.SelectOperationalRelays(date, pool, operationalCount);
        var outage = LunarWeatherRelaySelector.SelectOutageRelays(pool, operational);

        Assert.Equal(100, pool.Count);
        Assert.Equal(operationalCount, operational.Count);
        Assert.Equal(100 - operationalCount, outage.Count);
        Assert.Equal(operationalCount + outage.Count, pool.Count);
    }

    [Fact]
    public void ResolveOperationalCount_is_stable_for_same_date()
    {
        var date = new DateOnly(2026, 3, 15);
        var first = LunarWeatherRelaySelector.ResolveOperationalCount(date, DefaultOptions);
        var second = LunarWeatherRelaySelector.ResolveOperationalCount(date, DefaultOptions);
        Assert.Equal(first, second);
    }
}
