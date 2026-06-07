using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public static class LunarWeatherAdminSettingsValidator
{
    public static (AdminLunarWeatherSettingsValues? Values, string? Error) Validate(
        AdminUpdateLunarWeatherSettingsRequest request,
        int catalogTotal)
    {
        var maxPool = catalogTotal > 0 ? catalogTotal : 100;
        var relayPoolSize = Math.Clamp(request.RelayPoolSize, 1, maxPool);
        var target = Math.Clamp(request.TargetOperationalCount, 1, relayPoolSize);
        var variance = Math.Max(0, request.OperationalVariance);
        var min = Math.Clamp(request.MinOperationalCount, 1, relayPoolSize);
        var max = Math.Clamp(request.MaxOperationalCount, min, relayPoolSize);

        if (min > target)
        {
            return (null, "Minimum operational count cannot exceed the target.");
        }

        if (max < target)
        {
            return (null, "Maximum operational count cannot be less than the target.");
        }

        return (new AdminLunarWeatherSettingsValues(relayPoolSize, target, variance, min, max), null);
    }
}

public sealed record AdminLunarWeatherSettingsValues(
    int RelayPoolSize,
    int TargetOperationalCount,
    int OperationalVariance,
    int MinOperationalCount,
    int MaxOperationalCount);
