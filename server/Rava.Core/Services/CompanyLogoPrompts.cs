namespace Rava.Core.Services;

public static class CompanyLogoPrompts
{
    public static string Build(
        string companyName,
        string username,
        string mood,
        string aboutMe,
        string interests,
        string music)
    {
        var company = companyName.Trim();
        if (string.IsNullOrWhiteSpace(company) || company.Equals("No active mine", StringComparison.OrdinalIgnoreCase))
        {
            company = $"{username.Trim()} Mining";
        }

        mood = TrimSnippet(mood, 80);
        var about = TrimSnippet(aboutMe, 160);
        interests = TrimSnippet(interests, 120);
        music = TrimSnippet(music, 80);

        var profileHints = new List<string>();
        if (!string.IsNullOrWhiteSpace(mood))
        {
            profileHints.Add($"operator mood: {mood}");
        }

        if (!string.IsNullOrWhiteSpace(about))
        {
            profileHints.Add($"about: {about}");
        }

        if (!string.IsNullOrWhiteSpace(interests))
        {
            profileHints.Add($"interests: {interests}");
        }

        if (!string.IsNullOrWhiteSpace(music))
        {
            profileHints.Add($"now playing: {music}");
        }

        var hintText = profileHints.Count > 0
            ? string.Join("; ", profileHints)
            : "asteroid mining, ore haulers, frontier industry";

        return
            "Flat vector corporate emblem logo mark for a sci-fi asteroid mining company. " +
            $"Company name inspiration: \"{company}\". Brand personality from profile: {hintText}. " +
            "Single centered symbol or monogram, no photograph, no people, no scenery. " +
            "Bold simple shapes, cool cyan and steel blue palette with one accent color, readable at small size. " +
            "CRITICAL: fully transparent background (alpha channel), no backdrop, no border frame, no drop shadow on a rectangle, " +
            "no text, no letters, no words, no watermark.";
    }

    private static string TrimSnippet(string value, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength].TrimEnd() + "…";
    }
}
