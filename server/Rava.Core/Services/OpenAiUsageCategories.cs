namespace Rava.Core.Services;

/// <summary>Categories for outbound OpenAI HTTP calls (status dashboard breakdown).</summary>
public static class OpenAiUsageCategories
{
    public const string StoryGeneration = "story_generation";
    public const string ImageGeneration = "image_generation";
    public const string ReporterPortrait = "reporter_portrait";
    public const string CompanyLogo = "company_logo";
    public const string Other = "other";
}
