namespace Rava.Core.Configuration;

/// <summary>
/// Resolved hosting paths computed once at API startup (not loaded from appsettings.json).
/// </summary>
public sealed class RavaHostingPaths
{
    public required string DataRoot { get; init; }

    public required string ImagesRoot { get; init; }

    public required string OffworldNewsCacheRoot { get; init; }
}
