using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services;

/// <summary>Resolves OpenAI API keys and endpoints (global config with legacy per-feature fallbacks).</summary>
public sealed class OpenAiConnectionResolver(
    IOptions<OpenAiOptions> openAiOptions,
    IConfiguration configuration)
{
    private readonly OpenAiOptions _openAi = openAiOptions.Value;

    public string? ApiKey =>
        FirstNonEmpty(
            _openAi.ApiKey,
            configuration["OpenAi:ApiKey"],
            configuration["OffworldNews:ApiKey"],
            configuration["LunarWeather:ApiKey"],
            configuration["CompanyLogo:ApiKey"]);

    public string? AdminApiKey =>
        FirstNonEmpty(
            _openAi.AdminApiKey,
            configuration["OpenAi:AdminApiKey"],
            configuration["OffworldNews:AdminApiKey"]);

    public bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string BaseUrl =>
        FirstNonEmpty(_openAi.BaseUrl, configuration["OpenAi:BaseUrl"])
        ?? "https://api.openai.com/v1";

    public string TextModel =>
        FirstNonEmpty(_openAi.TextModel, configuration["OpenAi:TextModel"])
        ?? "gpt-4o-mini";

    public string ImageModel =>
        FirstNonEmpty(_openAi.ImageModel, configuration["OpenAi:ImageModel"])
        ?? "dall-e-3";

    public string BaseUrlForCompanyLogo(CompanyLogoOptions logo) =>
        FirstNonEmpty(logo.BaseUrl, BaseUrl) ?? BaseUrl;

    public string ImageModelForCompanyLogo(CompanyLogoOptions logo) =>
        FirstNonEmpty(logo.ImageModel, ImageModel) ?? ImageModel;

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
