using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsReporterSocialTests
{
    public OffworldNewsReporterSocialTests()
    {
        ReporterCatalogTestSupport.ConfigureFromTestOutput();
    }

    [Fact]
    public void ProfileNumber_is_stable_per_slug()
    {
        var reporter = OffworldNewsReporterCatalog.All[0];
        var first = OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug);
        var second = OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug);

        Assert.Equal(first, second);
        Assert.StartsWith("!ONN-", first, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetByProfileNumber_round_trips_catalog_slug()
    {
        var reporter = OffworldNewsReporterCatalog.All[3];
        var profileNumber = OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug);

        var found = OffworldNewsReporterSocial.TryGetByProfileNumber(profileNumber);

        Assert.NotNull(found);
        Assert.Equal(reporter.Slug, found.Slug);
    }

    [Fact]
    public void TryGetByUsername_matches_slug_and_display_name()
    {
        var reporter = OffworldNewsReporterCatalog.All[1];

        Assert.NotNull(OffworldNewsReporterSocial.TryGetByUsername(reporter.Slug));
        Assert.NotNull(OffworldNewsReporterSocial.TryGetByUsername(reporter.DisplayName));
    }

    [Fact]
    public void Profile_numbers_are_unique_across_roster()
    {
        var numbers = OffworldNewsReporterCatalog.All
            .Select(reporter => OffworldNewsReporterSocial.ProfileNumberFor(reporter.Slug))
            .ToList();

        Assert.Equal(numbers.Count, numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
