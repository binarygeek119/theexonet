namespace Theexonet.Api.Services.OffworldNews;

public sealed record OffworldNewsImageGenerationSummary(
    int Attempted,
    int Succeeded,
    string? LastError)
{
    public static OffworldNewsImageGenerationSummary Empty { get; } = new(0, 0, null);

    public string DescribeFailure() =>
        string.IsNullOrWhiteSpace(LastError)
            ? "All AI image requests failed. Check OpenAi.ApiKey, ImageModel, billing, and API logs."
            : LastError;
}
