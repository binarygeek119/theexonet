namespace Theexonet.Core.Constants;

public static class BanReasons
{
    private static readonly string[] AllPresets =
    [
        "Hate speech or harassment",
        "Threats or intimidation",
        "Spam or scam activity",
        "Cheating or exploiting game mechanics",
        "Impersonating staff or other players",
        "Inappropriate profile content",
        "Repeated warnings ignored",
        "Terms of service violation"
    ];

    public static IReadOnlyList<string> Presets => AllPresets;
}
