using Rava.Core.Dtos;

namespace Rava.Core.Services;

public static class OffworldNewsAdminSettingsValidator
{
    public static (OffworldNewsAdminSettingsValues? Values, string? Error) Validate(
        AdminUpdateOffworldNewsSettingsRequest request)
    {
        if (request.ReporterPoolSize < 0)
        {
            return (null, "Reporter pool size cannot be negative.");
        }

        if (request.StoriesPerDay < 1
            || request.MinStoriesPerDay < 1
            || request.MaxStoriesPerDay < 1)
        {
            return (null, "Story counts must be at least 1.");
        }

        if (request.StoriesPerDayVariance < 0)
        {
            return (null, "Story count variance cannot be negative.");
        }

        var target = request.StoriesPerDay;
        var variance = request.StoriesPerDayVariance;
        var min = request.MinStoriesPerDay;
        var max = request.MaxStoriesPerDay;

        if (min > target)
        {
            return (null, "Minimum stories per day cannot exceed the target.");
        }

        if (max < target)
        {
            return (null, "Maximum stories per day cannot be less than the target.");
        }

        if (min > max)
        {
            return (null, "Minimum stories per day cannot exceed the maximum.");
        }

        return (
            new OffworldNewsAdminSettingsValues(
                request.ReporterPoolSize,
                target,
                variance,
                min,
                max),
            null);
    }
}

public sealed record OffworldNewsAdminSettingsValues(
    int ReporterPoolSize,
    int StoriesPerDay,
    int StoriesPerDayVariance,
    int MinStoriesPerDay,
    int MaxStoriesPerDay);
