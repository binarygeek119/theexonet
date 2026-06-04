using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Core.Tests;

public class LunarWeatherAdminSettingsValidatorTests
{
    [Fact]
    public void Validate_accepts_typical_fuzzy_band()
    {
        var (values, error) = LunarWeatherAdminSettingsValidator.Validate(
            new AdminUpdateLunarWeatherSettingsRequest(100, 30, 5, 22, 38),
            catalogTotal: 100);

        Assert.Null(error);
        Assert.NotNull(values);
        Assert.Equal(30, values!.TargetOperationalCount);
        Assert.Equal(5, values.OperationalVariance);
    }

    [Fact]
    public void Validate_rejects_max_below_target()
    {
        var (_, error) = LunarWeatherAdminSettingsValidator.Validate(
            new AdminUpdateLunarWeatherSettingsRequest(50, 40, 3, 10, 25),
            catalogTotal: 100);

        Assert.Equal("Maximum operational count cannot be less than the target.", error);
    }
}
