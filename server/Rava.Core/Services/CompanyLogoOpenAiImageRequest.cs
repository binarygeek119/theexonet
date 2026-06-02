namespace Rava.Core.Services;

public static class CompanyLogoOpenAiImageRequest
{
    public static Dictionary<string, object> BuildRequestBody(string imageModel, string prompt)
    {
        var model = string.IsNullOrWhiteSpace(imageModel) ? "gpt-image-1" : imageModel.Trim();
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["size"] = OffworldNewsOpenAiImageRequest.ResolveSize(model, "1024x1024"),
            ["n"] = 1,
        };

        if (OffworldNewsOpenAiImageRequest.IsGptImageModel(model))
        {
            body["quality"] = "high";
            body["output_format"] = "png";
            body["background"] = "transparent";
            return body;
        }

        if (OffworldNewsOpenAiImageRequest.IsDalleModel(model))
        {
            body["response_format"] = "b64_json";
            return body;
        }

        body["response_format"] = "b64_json";
        return body;
    }
}
