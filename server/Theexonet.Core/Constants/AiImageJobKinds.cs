namespace Theexonet.Core.Constants;

public static class AiImageJobKinds
{
    public const string OnnEditionStories = "onn_edition_stories";
    public const string OnnReporterAvatar = "onn_reporter_avatar";
    public const string OnnReporterBackground = "onn_reporter_background";
    public const string OnnStoryImage = "onn_story_image";
    public const string ForeverfallIntake = "foreverfall_intake";
    public const string ForeverfallPortrait = "foreverfall_portrait";
    public const string LunarWeatherBulletin = "lunar_weather_bulletin";
    public const string VoidCorpProduct = "voidcorp_product";
    public const string TestingDummyAvatar = "testing_dummy_avatar";
    public const string TestingDummyBackground = "testing_dummy_background";
    public const string TestingDummyLogo = "testing_dummy_logo";
    public const string CompanyLogo = "company_logo";

    public static bool IsOnnReporterKind(string? kind) =>
        string.Equals(kind, OnnReporterAvatar, StringComparison.Ordinal)
        || string.Equals(kind, OnnReporterBackground, StringComparison.Ordinal);
}

public static class AiGenerationJobKinds
{
    public static bool IsText(string? kind) =>
        string.Equals(kind, AiImageJobKinds.OnnEditionStories, StringComparison.Ordinal)
        || string.Equals(kind, AiImageJobKinds.ForeverfallIntake, StringComparison.Ordinal)
        || string.Equals(kind, AiImageJobKinds.LunarWeatherBulletin, StringComparison.Ordinal);

    public static bool IsImage(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && !IsText(kind);

    public static IReadOnlyList<string> ResolveGroupKinds(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return [];
        }

        return group.ToLowerInvariant() switch
        {
            "onn" =>
            [
                AiImageJobKinds.OnnEditionStories,
                AiImageJobKinds.OnnReporterAvatar,
                AiImageJobKinds.OnnReporterBackground,
                AiImageJobKinds.OnnStoryImage,
            ],
            "foreverfall" =>
            [
                AiImageJobKinds.ForeverfallIntake,
                AiImageJobKinds.ForeverfallPortrait,
            ],
            "lunar_weather" => [AiImageJobKinds.LunarWeatherBulletin],
            "voidcorp" => [AiImageJobKinds.VoidCorpProduct],
            "testing" =>
            [
                AiImageJobKinds.TestingDummyAvatar,
                AiImageJobKinds.TestingDummyBackground,
                AiImageJobKinds.TestingDummyLogo,
            ],
            "company" => [AiImageJobKinds.CompanyLogo],
            _ => [],
        };
    }
}
