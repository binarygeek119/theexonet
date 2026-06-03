namespace Rava.Core.Configuration;

/// <summary>
/// Resolved hosting paths computed once at API startup (not loaded from appsettings.json).
/// </summary>
public sealed class RavaHostingPaths
{
    public required string DataRoot { get; init; }

    public required string ImagesRoot { get; init; }

    public required string OffworldNewsCacheRoot { get; init; }

    public required string WebRoot { get; init; }

    /// <summary>AI-generated reporter portraits and banners (persists under data/exonet in production).</summary>
    public string OffworldNewsReportersAssetsRoot =>
        Path.Combine(OffworldNewsCacheRoot, "reporters");

    /// <summary>AI-generated testing dummy miner profile assets.</summary>
    public string TestingDummyFriendsAssetsRoot =>
        Path.Combine(DataRoot, "exonet", "testing-dummy-friends");

    /// <summary>Primary data path plus legacy publish html folder for reporter JPEGs.</summary>
    public string[] ReporterAssetRoots() =>
    [
        OffworldNewsReportersAssetsRoot,
        Path.Combine(WebRoot, "exonet", "offworld-news", "reporters"),
    ];
}
