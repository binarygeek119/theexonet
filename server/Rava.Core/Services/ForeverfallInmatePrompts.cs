namespace Rava.Core.Services;

public static class ForeverfallInmatePrompts
{
    public const string SystemPrompt =
        """
        You write inmate intake dossiers for Foreverfall Penitentiary, a maximum-security black-hole prison in the RAVA sci-fi universe.
        Tone: Star Trek–like frontier justice — formal, slightly dry, but vivid. Keep it game-appropriate (no gore, no sexual content, no real-world politics).
        Species may be human or alien. Sentences are always galactic lifetime (until heat death, event-horizon labor, or similar).
        Do not mention AI, ChatGPT, or language models.
        Output valid JSON only.
        """;

    public static string BuildBatchPrompt(DateOnly intakeDate, int count, int maleCount, int femaleCount) =>
        $$"""
        Generate exactly {{count}} new inmate intake records for Foreverfall Penitentiary.
        Intake date: {{intakeDate:yyyy-MM-dd}}
        Gender split: {{maleCount}} male, {{femaleCount}} female (field gender must be "male" or "female").

        Rules:
        - Mix humans and aliens (roughly half alien).
        - Crimes: space piracy, unauthorized warp core trafficking, first-contact treaty violations, belt claim-jumping, corporate espionage, antimatter smuggling, falsifying jump clearance, hijacking ore convoys, sabotaging life support, illegal xeno-archaeology, etc.
        - intakeReason: one sentence why they arrived today (transfer, new conviction, bounty delivery).
        - crime: short charge label.
        - sentence: galactic lifetime phrasing (creative but always permanent).
        - bio: 2-3 sentences dossier summary.
        - displayName: full name or designation.
        - species: e.g. Human, Centauri, Andorian-like, insectoid, silicon-based, etc.

        Return JSON only:
        {
          "inmates": [
            {
              "displayName": "string",
              "species": "string",
              "gender": "male",
              "crime": "string",
              "sentence": "string",
              "intakeReason": "string",
              "bio": "string"
            }
          ]
        }
        """;

    public static string BuildPortraitPrompt(string displayName, string species, string gender) =>
        $"""
        Sci-fi prison mugshot portrait of {displayName}, a {species} ({gender}) inmate at Foreverfall Penitentiary,
        a maximum-security orbital prison near a black hole. Star Trek–inspired uniform jumpsuit, institutional backdrop,
        harsh blue-tinted lighting, space prison aesthetic, photorealistic illustration, head and shoulders, no text, no watermark.
        """;
}
