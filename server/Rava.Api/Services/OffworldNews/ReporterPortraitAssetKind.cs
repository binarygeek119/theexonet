namespace Rava.Api.Services.OffworldNews;

public enum ReporterPortraitAssetKind
{
    Both,
    Avatar,
    Background,
}

public static class ReporterPortraitAssetKindParser
{
    public static bool TryParse(string? value, out ReporterPortraitAssetKind kind, out string? error)
    {
        kind = ReporterPortraitAssetKind.Both;
        error = null;

        switch ((value ?? "both").Trim().ToLowerInvariant())
        {
            case "":
            case "both":
            case "all":
                kind = ReporterPortraitAssetKind.Both;
                return true;
            case "avatar":
            case "portrait":
            case "profile":
                kind = ReporterPortraitAssetKind.Avatar;
                return true;
            case "background":
            case "banner":
                kind = ReporterPortraitAssetKind.Background;
                return true;
            default:
                error = "assets must be avatar, background, or both.";
                return false;
        }
    }

    public static string Describe(ReporterPortraitAssetKind kind) =>
        kind switch
        {
            ReporterPortraitAssetKind.Avatar => "portrait",
            ReporterPortraitAssetKind.Background => "banner",
            _ => "portraits",
        };
}
