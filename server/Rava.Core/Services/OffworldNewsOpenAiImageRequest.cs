namespace Rava.Core.Services;

/// <summary>
/// Builds OpenAI /images/generations request bodies for DALL-E and GPT image models.
/// </summary>
public static class OffworldNewsOpenAiImageRequest
{
    public static bool IsGptImageModel(string? model) =>
        !string.IsNullOrWhiteSpace(model)
        && model.Trim().StartsWith("gpt-image", StringComparison.OrdinalIgnoreCase);

    public static bool IsDalleModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return true;
        }

        var normalized = model.Trim();
        return normalized.StartsWith("dall-e-2", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("dall-e-3", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps catalog aspect sizes to a size accepted by the configured image model.
    /// </summary>
    public static string ResolveSize(string? imageModel, string aspectApiSize)
    {
        var model = string.IsNullOrWhiteSpace(imageModel) ? "dall-e-3" : imageModel.Trim();

        if (IsGptImageModel(model) || !IsDalleModel(model))
        {
            return aspectApiSize switch
            {
                "1792x1024" => "1536x1024",
                "1024x1792" => "1024x1536",
                _ => "1024x1024",
            };
        }

        if (string.Equals(model, "dall-e-2", StringComparison.OrdinalIgnoreCase)
            && aspectApiSize is not ("256x256" or "512x512" or "1024x1024"))
        {
            return "1024x1024";
        }

        return aspectApiSize;
    }

    public static Dictionary<string, object> BuildRequestBody(string imageModel, string prompt, string aspectApiSize)
    {
        var model = string.IsNullOrWhiteSpace(imageModel) ? "dall-e-3" : imageModel.Trim();
        var size = ResolveSize(model, aspectApiSize);

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["size"] = size,
            ["n"] = 1,
        };

        if (IsGptImageModel(model))
        {
            // GPT image models always return base64; response_format is not supported.
            body["quality"] = "medium";
            body["output_format"] = "jpeg";
            return body;
        }

        if (string.Equals(model, "dall-e-3", StringComparison.OrdinalIgnoreCase))
        {
            body["quality"] = "standard";
            return body;
        }

        if (!IsDalleModel(model))
        {
            // Unknown / future model names: send only the common parameters.
            return body;
        }

        return body;
    }
}
