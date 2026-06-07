namespace Theexonet.Core.Services;

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
            "Professional corporate logo mark for a sci-fi asteroid mining company — a real brand emblem, not a photo or UI mockup. " +
            $"Inspired by the company name \"{company}\" and profile tone: {hintText}. " +
            "Design one centered icon only: geometric crest, ore-crystal badge, abstract hauler silhouette, or industrial monogram built from simple shapes. " +
            "Style like a Fortune 500 / aerospace company logo: clean vector art, balanced symmetry, bold readable silhouette, cool cyan and steel blue with one accent. " +
            "CRITICAL: fully transparent background (alpha channel only), clear art floating on transparency — no backdrop, no white box, no border frame, " +
            "no card, no drop-shadow rectangle, no paper, no scene, no people, no text, no letters, no words, no watermark.";
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
