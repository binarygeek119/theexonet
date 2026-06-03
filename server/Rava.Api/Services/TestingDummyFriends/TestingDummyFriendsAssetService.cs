using Rava.Core.Configuration;
using Rava.Core.Services;

namespace Rava.Api.Services.TestingDummyFriends;

public sealed class TestingDummyFriendsAssetService(
    RavaHostingPaths hostingPaths,
    TestingDummyFriendsAssetGenerator generator,
    ILogger<TestingDummyFriendsAssetService> logger)
{
    private readonly SemaphoreSlim _jobLock = new(1, 1);
    private int _running;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    public bool TryStartEnsureMissing()
    {
        if (!generator.IsConfigured)
        {
            return false;
        }

        if (IsRunning)
        {
            return false;
        }

        var missing = CountMissingAssets(hostingPaths.TestingDummyFriendsAssetsRoot);
        if (missing == 0)
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureMissingAssetsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Testing dummy asset ensure job failed.");
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        });

        return true;
    }

    public async Task<(int MissingBefore, int Attempted, int Succeeded, string? LastError)> EnsureMissingAssetsAsync(
        CancellationToken ct)
    {
        await _jobLock.WaitAsync(ct);
        try
        {
            var assetsRoot = hostingPaths.TestingDummyFriendsAssetsRoot;
            Directory.CreateDirectory(assetsRoot);

            var missingBefore = CountMissingAssets(assetsRoot);
            if (missingBefore == 0)
            {
                return (0, 0, 0, null);
            }

            var attempted = 0;
            var succeeded = 0;
            string? lastError = null;

            foreach (var profile in TestingDummyFriendsCatalog.All())
            {
                ct.ThrowIfCancellationRequested();
                var result = await generator.EnsureProfileAssetsAsync(profile, assetsRoot, ct);
                attempted += result.Attempted;
                succeeded += result.Succeeded;
                if (!string.IsNullOrWhiteSpace(result.LastError))
                {
                    lastError = result.LastError;
                }

                if (result.Attempted > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(8), ct);
                }
            }

            logger.LogInformation(
                "Testing dummy asset ensure finished. Missing before={MissingBefore}, attempted={Attempted}, succeeded={Succeeded}",
                missingBefore,
                attempted,
                succeeded);

            return (missingBefore, attempted, succeeded, lastError);
        }
        finally
        {
            _jobLock.Release();
        }
    }

    public static int CountMissingAssets(string assetsRoot)
    {
        var missing = 0;
        for (var index = 0; index < TestingDummyFriendsCatalog.DummyCount; index++)
        {
            if (!File.Exists(TestingDummyFriendsPaths.AvatarFilePath(assetsRoot, index)))
            {
                missing++;
            }

            if (!File.Exists(TestingDummyFriendsPaths.BackgroundFilePath(assetsRoot, index)))
            {
                missing++;
            }

            if (!File.Exists(TestingDummyFriendsPaths.LogoFilePath(assetsRoot, index)))
            {
                missing++;
            }
        }

        return missing;
    }
}
