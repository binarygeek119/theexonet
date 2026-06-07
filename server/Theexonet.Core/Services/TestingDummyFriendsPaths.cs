namespace Theexonet.Core.Services;

public static class TestingDummyFriendsPaths
{
    public const string PublicRootPath = "/exonet/testing-dummy-friends";

    public static string ProfileFolder(string assetsRoot, int index) =>
        Path.Combine(assetsRoot, index.ToString("D2"));

    public static string AvatarFilePath(string assetsRoot, int index) =>
        Path.Combine(ProfileFolder(assetsRoot, index), "avatar.jpg");

    public static string BackgroundFilePath(string assetsRoot, int index) =>
        Path.Combine(ProfileFolder(assetsRoot, index), "background.jpg");

    public static string LogoFilePath(string assetsRoot, int index) =>
        Path.Combine(ProfileFolder(assetsRoot, index), "logo.png");

    public static string AvatarUrl(int index) =>
        $"{PublicRootPath}/{index:D2}/avatar.jpg";

    public static string BackgroundUrl(int index) =>
        $"{PublicRootPath}/{index:D2}/background.jpg";

    public static string LogoUrl(int index) =>
        $"{PublicRootPath}/{index:D2}/logo.png";
}
