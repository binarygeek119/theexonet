using Microsoft.Extensions.Options;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.OpenAi;

public sealed class OpenAiStatusDetailService(
    OpenAiUsageTracker usageTracker,
    OpenAiBillingProbe billingProbe,
    OpenAiConnectionResolver openAi,
    IOptions<OffworldNewsOptions> offworldNewsOptions,
    IOptions<CompanyLogoOptions> companyLogoOptions,
    OffworldNewsAdminSettingsStore adminSettings,
    OffworldNewsService offworldNewsService,
    OffworldNewsReporterPortraitJobService portraitJobService)
{
    public async Task<PublicOpenAiStatusDetailResponse> BuildAsync(CancellationToken cancellationToken)
    {
        var offworld = offworldNewsOptions.Value;
        var logo = companyLogoOptions.Value;
        var usage = usageTracker.GetSnapshot();
        var billing = await billingProbe.GetCreditsAsync(cancellationToken);
        var apiKey = openAi.ApiKey;
        var apiKeyConfigured = openAi.IsApiKeyConfigured;

        var configuration = new PublicOpenAiConfigurationDto(
            offworld.Enabled,
            logo.Enabled,
            apiKeyConfigured,
            MaskApiKey(apiKey),
            openAi.BaseUrl,
            openAi.TextModel,
            openAi.ImageModel,
            offworld.StoriesPerDay,
            offworld.MaxImagesPerDay,
            false,
            openAi.ImageModelForCompanyLogo(logo),
            Math.Max(0, logo.SecondsBetweenGenerations),
            string.IsNullOrWhiteSpace(logo.BaseUrl) ? "(same as OpenAi.BaseUrl)" : logo.BaseUrl.Trim());

        var exonet = offworldNewsService.GetPublicAiSnapshot(
            adminSettings.ReporterPoolSize,
            adminSettings.ActivePoolCount(),
            portraitJobService.GetStatus());

        decimal? creditsUsed = null;
        if (billing.CreditsGrantedUsd is not null && billing.CreditsRemainingUsd is not null)
        {
            creditsUsed = billing.CreditsGrantedUsd.Value - billing.CreditsRemainingUsd.Value;
            if (creditsUsed < 0)
            {
                creditsUsed = 0;
            }
        }
        else if (billing.MonthToDateSpendUsd is not null)
        {
            creditsUsed = billing.MonthToDateSpendUsd;
        }

        return new PublicOpenAiStatusDetailResponse(
            DateTime.UtcNow,
            apiKeyConfigured,
            usage.TotalRequests,
            usage.SuccessfulRequests,
            usage.FailedRequests,
            usage.RequestsToday,
            usage.SuccessfulRequestsToday,
            usage.FailedRequestsToday,
            usage.RequestsByCategory,
            usage.SuccessfulRequestsByCategory,
            usage.FailedRequestsByCategory,
            usage.LastRequestUtc,
            billing.CreditsRemainingUsd,
            billing.CreditsGrantedUsd,
            creditsUsed,
            billing.Note,
            configuration,
            BuildGameFeatures(offworld, logo, openAi, apiKeyConfigured),
            exonet);
    }

    private static IReadOnlyList<PublicOpenAiGameFeatureDto> BuildGameFeatures(
        OffworldNewsOptions offworld,
        CompanyLogoOptions logo,
        OpenAiConnectionResolver openAi,
        bool apiKeyConfigured)
    {
        var aiActive = apiKeyConfigured && offworld.Enabled;
        return
        [
            new PublicOpenAiGameFeatureDto(
                "exonet-stories",
                "Exonet / Offworld News — daily headlines",
                $"GPT writes JSON story drafts for the in-game news feed (Exonet tab). Daily story count varies around {offworld.StoriesPerDay} ±{offworld.StoriesPerDayVariance} (max {offworld.MaxStoriesPerDay}, date-seeded). Falls back to templates when AI is off.",
                OpenAiUsageCategories.StoryGeneration,
                aiActive,
                openAi.TextModel),
            new PublicOpenAiGameFeatureDto(
                "exonet-images",
                "Exonet — story illustrations",
                "Image model renders up to MaxImagesPerDay AI illustrations per edition; other stories use category placeholders.",
                OpenAiUsageCategories.ImageGeneration,
                aiActive && offworld.MaxImagesPerDay > 0,
                openAi.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-avatars",
                "Reporter profile pictures",
                "AI head-and-shoulders portraits for Offworld News correspondents on Exonet.",
                OpenAiUsageCategories.ReporterAvatar,
                aiActive,
                openAi.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-backgrounds",
                "Reporter profile banners",
                "AI wide banner backgrounds for ONN bureau profiles (signature news locations).",
                OpenAiUsageCategories.ReporterBackground,
                aiActive,
                openAi.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-portraits-legacy",
                "Reporter portraits (legacy bucket)",
                "Older portrait jobs before avatar and banner were tracked separately.",
                OpenAiUsageCategories.ReporterPortrait,
                false,
                openAi.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "company-logos",
                "Player company logos",
                "Queued PNG logo generation from profile/company settings using the player’s company name and bio context.",
                OpenAiUsageCategories.CompanyLogo,
                apiKeyConfigured && logo.Enabled,
                openAi.ImageModelForCompanyLogo(logo)),
        ];
    }

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8)
        {
            return "••••";
        }

        return $"…{trimmed[^4..]}";
    }
}
