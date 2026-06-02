namespace Rava.Core.Services;

public static class OffworldNewsReporterPortraitPrompts
{
    public static string BuildAvatarPrompt(OffworldNewsReporterProfile reporter)
    {
        var beat = reporter.Beat.Trim();
        var bureau = reporter.Bureau.Trim();
        var personality = reporter.Personality.Trim();
        var subject = OffworldNewsReporterPortraitGender.PortraitSubjectPhrase(reporter.Gender);
        var article = subject.StartsWith('a') ? "an" : "a";

        return
            $"Editorial sci-fi news photograph, head-and-shoulders portrait of {article} {subject}, a frontier journalist. " +
            $"Subject evokes {personality} Beat: {beat}. Bureau: {bureau}. " +
            "Practical asteroid-mining frontier clothing, subtle comm headset or press badge, confident expression matching the personality. " +
            "Cinematic soft lighting, shallow depth of field, cool blue and cyan color grade, photorealistic, no text, no logos, no watermark.";
    }

    public static string BuildBackgroundPrompt(OffworldNewsReporterProfile reporter)
    {
        var beat = reporter.Beat.Trim();
        var bureau = reporter.Bureau.Trim();
        var personality = reporter.Personality.Trim();
        var location = OffworldNewsReporterBackgroundLocations.DescribeScene(reporter);

        return
            "Wide cinematic banner photograph for a sci-fi news network profile header. " +
            $"Iconic news location where this correspondent reports from: {location}. " +
            $"Mood and atmosphere: {personality}. {beat} beat journalism rooted at {bureau}. " +
            "Memorable field bureau or embed site, not a generic office. Empty scene, no people visible. " +
            "Cool blue and cyan color grade, photorealistic, no text, no logos, no watermark.";
    }
}
