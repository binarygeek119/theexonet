namespace Rava.Core.Services;

public static class VoidCorpProductPrompts
{
    public static string BuildImagePrompt(string displayName, string summary, string tagline) =>
        $"Professional product photograph of industrial asteroid mining equipment: {displayName}. "
        + $"VoidCorp frontier manufacturer aesthetic. {tagline}. "
        + $"Function: {summary}. Hard-science sci-fi, studio lighting on dark brushed metal surface, "
        + "photorealistic, no text, no logos, no watermarks, no people.";
}
