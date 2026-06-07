using Theexonet.Api.Services.OffworldNews;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Services.AiImageQueue;

public sealed class AiImageQueuePublisher(IServiceScopeFactory scopeFactory)
{
    public async Task<AiImageQueueEnqueueResult> EnqueueForeverfallPortraitsAsync(
        IReadOnlyList<ForeverfallPortraitJobItem> portraits,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiImageQueueService>().EnqueueManyAsync(
            portraits.Select(portrait => (
                AiImageJobKinds.ForeverfallPortrait,
                (object)new ForeverfallPortraitJobPayload(
                    portrait.ImageId,
                    portrait.DisplayName,
                    portrait.Species,
                    portrait.Gender))),
            source,
            ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueOnnReporterPortraitsAsync(
        IReadOnlyList<string>? slugs,
        ReporterPortraitAssetKind assets,
        string source,
        CancellationToken ct)
    {
        var targets = ResolveReporterSlugs(slugs);
        var jobs = new List<(string Kind, object Payload)>();
        foreach (var slug in targets)
        {
            if (assets is ReporterPortraitAssetKind.Both or ReporterPortraitAssetKind.Avatar)
            {
                jobs.Add((AiImageJobKinds.OnnReporterAvatar, new OnnReporterPortraitJobPayload(slug)));
            }

            if (assets is ReporterPortraitAssetKind.Both or ReporterPortraitAssetKind.Background)
            {
                jobs.Add((AiImageJobKinds.OnnReporterBackground, new OnnReporterPortraitJobPayload(slug)));
            }
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiImageQueueService>().EnqueueManyAsync(
            jobs,
            source,
            ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueOnnEditionStoriesAsync(
        DateOnly editionDate,
        bool forceRegenerate,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        var payload = new OnnEditionStoriesJobPayload(editionDate.ToString("yyyy-MM-dd"), forceRegenerate);
        return forceRegenerate
            ? await queue.EnqueueAsync(AiImageJobKinds.OnnEditionStories, payload, source, ct)
            : await queue.EnqueueUniqueAsync(AiImageJobKinds.OnnEditionStories, payload, source, ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueForeverfallIntakeAsync(
        DateOnly intakeDate,
        bool forceRegenerate,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        var payload = new ForeverfallIntakeJobPayload(intakeDate.ToString("yyyy-MM-dd"), forceRegenerate);
        return forceRegenerate
            ? await queue.EnqueueAsync(AiImageJobKinds.ForeverfallIntake, payload, source, ct)
            : await queue.EnqueueUniqueAsync(AiImageJobKinds.ForeverfallIntake, payload, source, ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueLunarWeatherBulletinAsync(
        DateOnly bulletinDate,
        bool forceRegenerate,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        var payload = new LunarWeatherBulletinJobPayload(bulletinDate.ToString("yyyy-MM-dd"), forceRegenerate);
        return forceRegenerate
            ? await queue.EnqueueAsync(AiImageJobKinds.LunarWeatherBulletin, payload, source, ct)
            : await queue.EnqueueUniqueAsync(AiImageJobKinds.LunarWeatherBulletin, payload, source, ct);
    }

    public async Task<AdminAiImageQueueStatusDto> GetStatusAsync(string? kind, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<AiImageQueueService>()
            .GetStatusAsync(kind, ct);
    }

    public async Task<AiGenerationQueueWaitResult> WaitForJobAsync(
        string kind,
        string source,
        TimeSpan timeout,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<AiImageQueueService>()
            .WaitForCompletionAsync(kind, source, timeout, ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueOnnStoryImagesAsync(
        DateOnly editionDate,
        IReadOnlyList<(string StoryId, int StoryIndex, string? ImagePrompt)> stories,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiImageQueueService>().EnqueueManyAsync(
            stories.Select(story => (
                AiImageJobKinds.OnnStoryImage,
                (object)new OnnStoryImageJobPayload(
                    editionDate.ToString("yyyy-MM-dd"),
                    story.StoryId,
                    story.StoryIndex,
                    story.ImagePrompt))),
            source,
            ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueVoidCorpProductsAsync(
        IEnumerable<string> slugs,
        string source,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiImageQueueService>().EnqueueManyAsync(
            slugs.Select(slug => (AiImageJobKinds.VoidCorpProduct, (object)new VoidCorpProductJobPayload(slug))),
            source,
            ct);
    }

    public async Task<AiImageQueueEnqueueResult> EnqueueMissingTestingDummyAssetsAsync(
        string assetsRoot,
        string source,
        CancellationToken ct)
    {
        var jobs = new List<(string Kind, object Payload)>();
        foreach (var profile in TestingDummyFriendsCatalog.All())
        {
            if (!File.Exists(TestingDummyFriendsPaths.AvatarFilePath(assetsRoot, profile.Index)))
            {
                jobs.Add((AiImageJobKinds.TestingDummyAvatar, new TestingDummyAssetJobPayload(profile.Index)));
            }

            if (!File.Exists(TestingDummyFriendsPaths.BackgroundFilePath(assetsRoot, profile.Index)))
            {
                jobs.Add((AiImageJobKinds.TestingDummyBackground, new TestingDummyAssetJobPayload(profile.Index)));
            }

            if (!File.Exists(TestingDummyFriendsPaths.LogoFilePath(assetsRoot, profile.Index)))
            {
                jobs.Add((AiImageJobKinds.TestingDummyLogo, new TestingDummyAssetJobPayload(profile.Index)));
            }
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AiImageQueueService>().EnqueueManyAsync(
            jobs,
            source,
            ct);
    }

    private static IReadOnlyList<string> ResolveReporterSlugs(IReadOnlyList<string>? slugs)
    {
        if (slugs is null || slugs.Count == 0)
        {
            return OffworldNewsReporterCatalog.All.Select(reporter => reporter.Slug).ToList();
        }

        return slugs
            .Where(slug => OffworldNewsReporterCatalog.TryGetBySlug(slug) is not null)
            .Select(slug => slug.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
