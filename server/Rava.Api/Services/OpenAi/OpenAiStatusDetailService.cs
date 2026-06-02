using Microsoft.Extensions.Options;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Services.OpenAi;

public sealed class OpenAiStatusDetailService(
    OpenAiUsageTracker usageTracker,
    OpenAiBillingProbe billingProbe,
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
        var apiKey = ResolveApiKey(offworld, logo);
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(apiKey);

        var configuration = new PublicOpenAiConfigurationDto(
            offworld.Enabled,
            logo.Enabled,
            apiKeyConfigured,
            MaskApiKey(apiKey),
            string.IsNullOrWhiteSpace(offworld.BaseUrl) ? "https://api.openai.com/v1" : offworld.BaseUrl.Trim(),
            offworld.TextModel,
            offworld.ImageModel,
            offworld.StoriesPerDay,
            offworld.MaxImagesPerDay,
            !string.IsNullOrWhiteSpace(logo.ApiKey),
            string.IsNullOrWhiteSpace(logo.ImageModel) ? "gpt-image-1" : logo.ImageModel,
            Math.Max(0, logo.SecondsBetweenGenerations),
            string.IsNullOrWhiteSpace(logo.BaseUrl) ? "(same as Offworld News)" : logo.BaseUrl.Trim());

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
            BuildGameFeatures(offworld, logo, apiKeyConfigured),
            exonet);
    }

    private static IReadOnlyList<PublicOpenAiGameFeatureDto> BuildGameFeatures(
        OffworldNewsOptions offworld,
        CompanyLogoOptions logo,
        bool apiKeyConfigured)
    {
        var aiActive = apiKeyConfigured && offworld.Enabled;
        return
        [
            new PublicOpenAiGameFeatureDto(
                "exonet-stories",
                "Exonet / Offworld News — daily headlines",
                "GPT writes JSON story drafts for the in-game news feed (Exonet tab). Falls back to template headlines when AI is off.",
                OpenAiUsageCategories.StoryGeneration,
                aiActive,
                offworld.TextModel),
            new PublicOpenAiGameFeatureDto(
                "exonet-images",
                "Exonet — story illustrations",
                "Image model renders up to MaxImagesPerDay AI illustrations per edition; other stories use category placeholders.",
                OpenAiUsageCategories.ImageGeneration,
                aiActive && offworld.MaxImagesPerDay > 0,
                offworld.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-avatars",
                "Reporter profile pictures",
                "AI head-and-shoulders portraits for Offworld News correspondents on Exonet.",
                OpenAiUsageCategories.ReporterAvatar,
                aiActive,
                offworld.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-backgrounds",
                "Reporter profile banners",
                "AI wide banner backgrounds for ONN bureau profiles (signature news locations).",
                OpenAiUsageCategories.ReporterBackground,
                aiActive,
                offworld.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "reporter-portraits-legacy",
                "Reporter portraits (legacy bucket)",
                "Older portrait jobs before avatar and banner were tracked separately.",
                OpenAiUsageCategories.ReporterPortrait,
                false,
                offworld.ImageModel),
            new PublicOpenAiGameFeatureDto(
                "company-logos",
                "Player company logos",
                "Queued PNG logo generation from profile/company settings using the player’s company name and bio context.",
                OpenAiUsageCategories.CompanyLogo,
                apiKeyConfigured && logo.Enabled,
                logo.ImageModel),
        ];
    }

    private static string? ResolveApiKey(OffworldNewsOptions offworld, CompanyLogoOptions logo) =>
        !string.IsNullOrWhiteSpace(offworld.ApiKey)
            ? offworld.ApiKey
            : logo.ApiKey;

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
