namespace Rava.Core.Services;

public static class TestingDummyFriendsPortraitPrompts
{
    public static string BuildAvatarPrompt(TestingDummyFriendsProfile profile)
    {
        var mood = profile.Mood.Trim();
        var about = profile.AboutMe.Trim();
        var interests = profile.Interests.Trim();

        return
            "Editorial sci-fi photograph, head-and-shoulders portrait of an adult asteroid belt miner and independent claim operator. " +
            $"Handle \"{profile.Username}\". Personality and current mood: {mood}. Background story: {about}. " +
            $"Personal interests shaping their look: {interests}. " +
            "Practical mining exosuit or rugged frontier work gear with subtle company patches, dust-scuffed helmet ring or comm rig, confident expression matching the mood. " +
            "Photorealistic face and grooming, believable human miner (not a news anchor, not a reporter). " +
            "Cinematic soft lighting, shallow depth of field, cool cyan and steel blue color grade, photorealistic, no text, no logos, no watermark.";
    }

    public static string BuildBackgroundPrompt(TestingDummyFriendsProfile profile)
    {
        var scene = TestingDummyFriendsBackgroundScenes.DescribeScene(profile);

        return
            "Wide cinematic banner photograph for a sci-fi asteroid miner's profile header. " +
            $"Scene at {profile.MineName}: {scene}. " +
            $"Mood: {profile.Mood.Trim()}. Operator interests: {profile.Interests.Trim()}. " +
            "Believable asteroid mining environment — claim site, drill rig, ore conveyor, habitat dome, or cargo bay suited to a player profile banner. " +
            "Empty scene, no people visible. Cool cyan and steel blue color grade, photorealistic, no text, no logos, no watermark.";
    }
}

internal static class TestingDummyFriendsBackgroundScenes
{
    private static readonly string[] Scenes =
    [
        "a drill tower lit by work lamps against a spinning rock face with ore streaks",
        "a cargo airlock bay stacked with sealed ore crates and mag-clamps",
        "a wide claim pad with survey beacons and a parked hauler silhouette",
        "a habitat dome viewport overlooking a fractured ridge rich with vein glow",
        "a maintenance catwalk above autonomous ore processors and dust haze",
        "a convoy staging field with fuel cells and tethered supply skiffs",
        "a deep-core access shaft rim with warning lights and ferroxite glint",
        "a quiet night shift yard with parked exosuits and tool racks",
        "a refinery intake chute and conveyor under harsh industrial floodlights",
        "a mapped tunnel mouth with geological core samples on display racks",
        "a union bulletin board wall beside a mess hall viewport to the void",
        "a company flagpole frame without cloth, set against a rich claim horizon",
    ];

    public static string DescribeScene(TestingDummyFriendsProfile profile)
    {
        var seed = $"dummy-bg-{profile.Index}-{profile.Username}";
        uint hash = 0;
        foreach (var ch in seed)
        {
            hash = hash * 31 + ch;
        }

        return Scenes[(int)(hash % (uint)Scenes.Length)];
    }
}
