using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class LunarWeatherTemplateGeneratorTests
{
    [Fact]
    public void Generate_produces_space_like_readings_not_earth_weather()
    {
        var date = new DateOnly(2026, 6, 4);
        var pool = LunarWeatherRelayCatalog.BuildDefaultPool();
        var options = new LunarWeatherOptions();
        var operational = LunarWeatherRelaySelector.SelectOperationalRelays(date, pool, 30);
        var outage = LunarWeatherRelaySelector.SelectOutageRelays(pool, operational);

        var bulletin = LunarWeatherTemplateGenerator.Generate(date, options, operational, outage, 30);

        Assert.Equal(30, bulletin.Readings.Count);
        Assert.Equal(70, bulletin.Outages.Count);
        Assert.All(bulletin.Readings, reading =>
        {
            Assert.False(string.IsNullOrWhiteSpace(reading.Summary));
            Assert.Contains(
                reading.Conditions,
                condition =>
                    condition.Contains("flux", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("radiation", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("plasma", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("vacuum", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("solar", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("micrometeor", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("magnetopause", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("regolith", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("dust", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("debris", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("ion", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("cryo", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("flare", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("coma", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("thermal", StringComparison.OrdinalIgnoreCase)
                    || condition.Contains("neutron", StringComparison.OrdinalIgnoreCase));
            var combined = string.Join(' ', reading.Conditions) + reading.Summary + reading.PressureNote;
            Assert.DoesNotContain("rain", combined, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("humidity", combined, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("snow", combined, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void BuildOutages_assigns_issues_from_pool()
    {
        var date = new DateOnly(2026, 1, 1);
        var pool = LunarWeatherRelayCatalog.BuildDefaultPool();
        var operational = LunarWeatherRelaySelector.SelectOperationalRelays(date, pool, 25);
        var outage = LunarWeatherRelaySelector.SelectOutageRelays(pool, operational);
        var outages = LunarWeatherTemplateGenerator.BuildOutages(date, outage);

        Assert.Equal(75, outages.Count);
        Assert.All(outages, entry => Assert.Contains(entry.Issue, LunarWeatherOutageReasons.All));
    }
}
