using Rava.Core.Dtos;

namespace Rava.Core.Services;

public static class OffworldNewsAdminSettingsValidator
{
    public const int AbsoluteMaxStoriesPerDay = 10;

    public static (OffworldNewsAdminSettingsValues? Values, string? Error) Validate(
        AdminUpdateOffworldNewsSettingsRequest request)
    {
        if (request.ReporterPoolSize < 0)
        {
            return (null, "Reporter pool size cannot be negative.");
        }

        var maxStories = Math.Clamp(request.MaxStoriesPerDay, 1, AbsoluteMaxStoriesPerDay);
        var target = Math.Clamp(request.StoriesPerDay, 1, maxStories);
        var variance = Math.Max(0, request.StoriesPerDayVariance);
        var min = Math.Clamp(request.MinStoriesPerDay, 1, maxStories);
        var max = Math.Clamp(request.MaxStoriesPerDay, min, AbsoluteMaxStoriesPerDay);

        if (min > target)
        {
            return (null, "Minimum stories per day cannot exceed the target.");
        }

        if (max < target)
        {
            return (null, "Maximum stories per day cannot be less than the target.");
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
