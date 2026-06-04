namespace Rava.Core.Services;

/// <summary>Categories for outbound OpenAI HTTP calls (status dashboard breakdown).</summary>
public static class OpenAiUsageCategories
{
    public const string StoryGeneration = "story_generation";
    public const string LunarWeather = "lunar_weather";
    public const string ImageGeneration = "image_generation";
    public const string ReporterPortrait = "reporter_portrait";
    public const string ReporterAvatar = "reporter_avatar";
    public const string ReporterBackground = "reporter_background";
    public const string CompanyLogo = "company_logo";
    public const string TestingDummyAvatar = "testing_dummy_avatar";
    public const string TestingDummyBackground = "testing_dummy_background";
    public const string TestingDummyLogo = "testing_dummy_logo";
    public const string Other = "other";
}
