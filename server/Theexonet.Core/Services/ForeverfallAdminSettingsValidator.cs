using Theexonet.Core.Dtos;

namespace Theexonet.Core.Services;

public static class ForeverfallAdminSettingsValidator
{
    public static (AdminForeverfallSettingsValues? Values, string? Error) Validate(
        AdminUpdateForeverfallSettingsRequest request)
    {
        var maxImages = Math.Clamp(request.MaxInmateImages, 1, 5000);
        var retentionDays = Math.Clamp(request.RetentionDays, 1, 365);
        var target = Math.Clamp(request.TargetDailyIntake, 1, maxImages);
        var variance = Math.Max(0, request.IntakeVariance);
        var min = Math.Clamp(request.MinDailyIntake, 1, maxImages);
        var max = Math.Clamp(request.MaxDailyIntake, min, maxImages);

        if (min > target)
        {
            return (null, "Minimum daily intake cannot exceed the target.");
        }

        if (max < target)
        {
            return (null, "Maximum daily intake cannot be less than the target.");
        }

        return (
            new AdminForeverfallSettingsValues(
                request.Enabled,
                maxImages,
                retentionDays,
                target,
                variance,
                min,
                max),
            null);
    }
}

public sealed record AdminForeverfallSettingsValues(
    bool Enabled,
    int MaxInmateImages,
    int RetentionDays,
    int TargetDailyIntake,
    int IntakeVariance,
    int MinDailyIntake,
    int MaxDailyIntake);
