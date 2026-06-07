using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.OpenAi;

public sealed class OpenAiStatusDetailService(
    OpenAiUsageTracker usageTracker,
    OpenAiBillingProbe billingProbe,
    OpenAiConnectionResolver openAi,
    IOptions<OffworldNewsOptions> offworldNewsOptions,
    IOptions<CompanyLogoOptions> companyLogoOptions,
    OffworldNewsAdminSettingsStore adminSettings,
    OffworldNewsService offworldNewsService,
    IServiceScopeFactory scopeFactory)
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

        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        var portraitJob = await queue.GetStatusAsync("onn_reporter", cancellationToken);

        var exonet = offworldNewsService.GetPublicAiSnapshot(
            adminSettings.ReporterPoolSize,
            adminSettings.ActivePoolCount(),
            portraitJob);

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

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8)
        {
            return "****";
        }

        return $"{trimmed[..4]}…{trimmed[^4..]}";
    }

    private static IReadOnlyList<PublicOpenAiGameFeatureDto> BuildGameFeatures(
        OffworldNewsOptions offworld,
        CompanyLogoOptions logo,
        OpenAiConnectionResolver openAi,
        bool apiKeyConfigured) =>
    [
        new(
            "offworld-news-stories",
            "Offworld News stories",
            "Daily AI-generated frontier news editions.",
            OpenAiUsageCategories.StoryGeneration,
            offworld.Enabled && apiKeyConfigured,
            openAi.TextModel),
        new(
            "offworld-news-images",
            "Offworld News story images",
            "Illustrations for daily Offworld News stories.",
            OpenAiUsageCategories.ImageGeneration,
            offworld.Enabled && apiKeyConfigured,
            openAi.ImageModel),
        new(
            "reporter-avatar",
            "Reporter avatars",
            "ONN correspondent profile portraits.",
            OpenAiUsageCategories.ReporterAvatar,
            offworld.Enabled && apiKeyConfigured,
            openAi.ImageModel),
        new(
            "reporter-background",
            "Reporter banners",
            "ONN correspondent profile banner images.",
            OpenAiUsageCategories.ReporterBackground,
            offworld.Enabled && apiKeyConfigured,
            openAi.ImageModel),
        new(
            "reporter-portraits-legacy",
            "Reporter portraits (legacy bucket)",
            "Older portrait jobs before avatar and banner were tracked separately.",
            OpenAiUsageCategories.ReporterPortrait,
            false,
            openAi.ImageModel),
        new(
            "company-logo",
            "Company logos",
            "AI-generated player company logos.",
            OpenAiUsageCategories.CompanyLogo,
            logo.Enabled && apiKeyConfigured,
            openAi.ImageModelForCompanyLogo(logo)),
    ];
}
