using Rava.Core.Services;

namespace Rava.Api.Services.OpenAi;

public sealed class OpenAiUsageLoggingHandler(OpenAiUsageTracker tracker) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<string> CategoryKey = new("Rava.OpenAiUsageCategory");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var category = request.Options.TryGetValue(CategoryKey, out var value) ? value : null;
        tracker.RecordRequest(category);
        return await base.SendAsync(request, cancellationToken);
    }

    public static void SetCategory(HttpRequestMessage request, string category) =>
        request.Options.Set(CategoryKey, category);
}
