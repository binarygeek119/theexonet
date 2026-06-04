using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class OffworldNewsAdminSettingsValidatorTests
{
    [Fact]
    public void Validate_accepts_typical_fuzzy_story_band()
    {
        var (values, error) = OffworldNewsAdminSettingsValidator.Validate(
            new AdminUpdateOffworldNewsSettingsRequest(0, 5, 3, 2, 8));

        Assert.Null(error);
        Assert.NotNull(values);
        Assert.Equal(5, values!.StoriesPerDay);
        Assert.Equal(3, values.StoriesPerDayVariance);
    }

    [Fact]
    public void Validate_rejects_min_above_target()
    {
        var (_, error) = OffworldNewsAdminSettingsValidator.Validate(
            new AdminUpdateOffworldNewsSettingsRequest(0, 3, 1, 5, 10));

        Assert.Equal("Minimum stories per day cannot exceed the target.", error);
    }

    [Fact]
    public void Validate_accepts_high_maximum_stories_per_day()
    {
        var (values, error) = OffworldNewsAdminSettingsValidator.Validate(
            new AdminUpdateOffworldNewsSettingsRequest(0, 15, 5, 10, 25));

        Assert.Null(error);
        Assert.Equal(25, values!.MaxStoriesPerDay);
    }
}
