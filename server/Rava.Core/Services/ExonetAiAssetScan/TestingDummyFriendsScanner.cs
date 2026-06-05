namespace Rava.Core.Services.ExonetAiAssetScan;

public sealed class TestingDummyFriendsScanner : IExonetAiAssetScanner
{
    public string AreaName => "TestingDummyFriends";

    public ExonetAiAssetScanAreaResult Scan(ExonetAiAssetScanContext context)
    {
        var assetsRoot = context.HostingPaths.TestingDummyFriendsAssetsRoot;
        var missing = TestingDummyFriendsAssetInventory.CountMissingAssets(assetsRoot);

        return new ExonetAiAssetScanAreaResult(AreaName, Missing: missing);
    }
}

public static class TestingDummyFriendsAssetInventory
{
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
