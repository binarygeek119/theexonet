namespace Rava.Core.Constants;

/// <summary>Built-in profile photo silhouettes when the player has not uploaded a custom avatar.</summary>
public static class ProfileAvatarPresets
{
    public const string Male = "male";
    public const string Female = "female";
    public const string Neutral = "neutral";

    public const string DefaultPreset = Neutral;
    public const string PublicPath = "/images/profile-defaults";

    public static IReadOnlyList<string> All { get; } = [Male, Female, Neutral];

    public static string Normalize(string? preset) =>
        preset?.Trim().ToLowerInvariant() switch
        {
            Male => Male,
            Female => Female,
            Neutral => Neutral,
            _ => DefaultPreset,
        };

    public static string AssetPath(string preset) =>
        $"{PublicPath}/{Normalize(preset)}.svg";

    public static bool HasCustomUpload(string? profileImageUrl) =>
        !string.IsNullOrWhiteSpace(profileImageUrl);

    public static string ResolveDisplayUrl(string? profileImageUrl, int revision, string preset)
    {
        if (!HasCustomUpload(profileImageUrl))
        {
            return AssetPath(preset);
        }

        var url = profileImageUrl!;
        return revision > 0 ? $"{url}?v={revision}" : url;
    }
}
