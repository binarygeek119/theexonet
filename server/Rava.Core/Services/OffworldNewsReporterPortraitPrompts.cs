namespace Rava.Core.Services;

public static class OffworldNewsReporterPortraitPrompts
{
    public static string BuildAvatarPrompt(OffworldNewsReporterProfile reporter)
    {
        var beat = reporter.Beat.Trim();
        var bureau = reporter.Bureau.Trim();
        var personality = reporter.Personality.Trim();

        return
            "Editorial sci-fi news photograph, head-and-shoulders portrait of a frontier journalist. " +
            $"Subject evokes {personality} Beat: {beat}. Bureau: {bureau}. " +
            "Practical asteroid-mining frontier clothing, subtle comm headset or press badge, confident expression matching the personality. " +
            "Cinematic soft lighting, shallow depth of field, cool blue and cyan color grade, photorealistic, no text, no logos, no watermark.";
    }

    public static string BuildBackgroundPrompt(OffworldNewsReporterProfile reporter)
    {
        var beat = reporter.Beat.Trim();
        var bureau = reporter.Bureau.Trim();
        var personality = reporter.Personality.Trim();

        return
            "Wide cinematic banner photograph for a sci-fi news network profile header. " +
            $"Setting at {bureau}, mood and props suggesting {beat} coverage, atmosphere: {personality}. " +
            "Viewport windows to asteroid belt or station interior, monitors with abstract charts, empty desk, no people. " +
            "Cool blue and cyan color grade, photorealistic, no text, no logos, no watermark.";
    }
}
