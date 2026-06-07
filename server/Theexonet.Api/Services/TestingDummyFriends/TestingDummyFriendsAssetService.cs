using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Core.Configuration;
using Theexonet.Core.Services;
using Theexonet.Core.Services.ExonetAiAssetScan;
namespace Theexonet.Api.Services.TestingDummyFriends;

public sealed class TestingDummyFriendsAssetService(
    TheexonetHostingPaths hostingPaths,
    TestingDummyFriendsAssetGenerator generator,
    AiImageQueuePublisher aiImageQueuePublisher,
    ILogger<TestingDummyFriendsAssetService> logger)
{
    public bool IsRunning => false;

    public bool TryStartEnsureMissing()
    {
        if (!generator.IsConfigured)
        {
            return false;
        }

        var missing = CountMissingAssets(hostingPaths.TestingDummyFriendsAssetsRoot);
        if (missing == 0)
        {
            return false;
        }

        _ = EnqueueMissingAsync(CancellationToken.None);
        return true;
    }

    public async Task<(int MissingBefore, int Attempted, int Succeeded, string? LastError)> EnsureMissingAssetsAsync(
        CancellationToken ct)
    {
        var assetsRoot = hostingPaths.TestingDummyFriendsAssetsRoot;
        Directory.CreateDirectory(assetsRoot);

        var missingBefore = CountMissingAssets(assetsRoot);
        if (missingBefore == 0)
        {
            return (0, 0, 0, null);
        }

        var result = await aiImageQueuePublisher.EnqueueMissingTestingDummyAssetsAsync(
            assetsRoot,
            "admin:testing-dummy-assets",
            ct);

        logger.LogInformation(
            "Testing dummy asset ensure queued {Count} job(s). Missing before={MissingBefore}",
            result.EnqueuedCount,
            missingBefore);

        return (missingBefore, result.EnqueuedCount, 0, result.Message);
    }

    private Task EnqueueMissingAsync(CancellationToken ct) =>
        aiImageQueuePublisher.EnqueueMissingTestingDummyAssetsAsync(
            hostingPaths.TestingDummyFriendsAssetsRoot,
            "auto:testing-dummy-assets",
            ct);

    public static int CountMissingAssets(string assetsRoot) =>
        TestingDummyFriendsAssetInventory.CountMissingAssets(assetsRoot);
}
