namespace Theexonet.Core.Constants;

/// <summary>
/// Public game version shown in the API, status dashboard, and game login screen.
/// Semantic versioning: MAJOR.MINOR.PATCH — bump major for major releases,
/// minor for new features, patch for bug fixes.
/// </summary>
public static class GameVersion
{
    public const int Major = 1;

    public const int Minor = 0;

    public const int Patch = 0;

    public static string Number => $"{Major}.{Minor}.{Patch}";

    public static string Display => $"V{Major}.{Minor}.{Patch}";
}
