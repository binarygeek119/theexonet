using System.Globalization;

namespace Theexonet.Core.Services;

public sealed record TestingDummyFriendsProfile(
    int Index,
    Guid PlayerId,
    string Username,
    string ProfileNumber,
    string Mood,
    string AboutMe,
    string Interests,
    string Music,
    string MineName,
    string AvatarUrl,
    string BackgroundUrl,
    string LogoUrl);

/// <summary>
/// Deterministic synthetic miner profiles for admin testing mode.
/// Must stay in sync with html/js/admin-testing-mode.js.
/// </summary>
public static class TestingDummyFriendsCatalog
{
    public const int DummyCount = 12;
    private const string DummyPlayerIdPrefix = "aaaaaaaa-aaaa-4aaa-8aaa-";
    private const string DummyFriendshipIdPrefix = "bbbbbbbb-bbbb-4bbb-8bbb-";

    private static readonly string[] Usernames =
    [
        "vein_runner",
        "ore_hauler_7",
        "void_shift",
        "ridge_claim",
        "nova_digger",
        "basalt_jax",
        "titan_bore",
        "lunar_haul",
        "cobalt_sable",
        "fault_line",
        "pulse_ore",
        "zenith_claim",
    ];

    private static readonly string[] Moods =
    [
        "Drilling through the quiet shift.",
        "Ore prices up, morale questionable.",
        "Living on freeze-dried coffee.",
        "Just hit a rich ferroxite pocket.",
        "Union meeting at 1800 ship time.",
        "Cataloging asteroid samples.",
    ];

    private static readonly string[] AboutSnippets =
    [
        "Third-gen belt miner. I run tight crews and tighter ledgers.",
        "Ex-corporate geologist gone independent. Ask me about vein mapping.",
        "Shipping surplus ore when the market hiccups. DMs open for convoy runs.",
        "Building a small syndicate one claim at a time.",
    ];

    private static readonly string[] Interests =
    [
        "Asteroid geology, vintage exosuits, belt hockey",
        "Market arbitrage, drone mods, synthwave",
        "Crew management, hazard pay debates, noodle bars",
        "Deep-core surveys, poker, ONN gossip",
    ];

    private static readonly string[] Music =
    [
        "Dust Choir — Beltline Echoes",
        "Static Haul — Night Shift 9",
        "Ferroxite Sons — Core Sample",
        "Orbital Lull — Slow Burn",
    ];

    private static readonly string[] MinePrefixes = ["Orion", "Stellar", "Nova", "Apex", "Deep", "Void", "Iron", "Quartz"];
    private static readonly string[] MineCores = ["Vein", "Dig", "Drill", "Ore", "Claim", "Shaft", "Forge", "Bore"];
    private static readonly string[] MineSuffixes = ["Co.", "Corp", "Works", "Syndicate", "Excavation", "Holdings"];

    public static bool TryGetIndexByUsername(string? usernameOrId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(usernameOrId))
        {
            return false;
        }

        var normalized = usernameOrId.Trim().ToLowerInvariant();
        for (var i = 0; i < DummyCount; i++)
        {
            if (Usernames[i].Equals(normalized, StringComparison.Ordinal))
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetIndexByPlayerId(string? playerId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(playerId) || !playerId.StartsWith(DummyPlayerIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(playerId[^12..], NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
        {
            return false;
        }

        return index >= 0 && index < DummyCount;
    }

    public static TestingDummyFriendsProfile Get(int index)
    {
        if (index < 0 || index >= DummyCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var seed = $"dummy-profile-{index}";
        return new TestingDummyFriendsProfile(
            index,
            DummyPlayerId(index),
            Usernames[index],
            DummyProfileNumber(index),
            Moods[Pick($"{seed}-mood", Moods.Length)],
            AboutSnippets[Pick($"{seed}-about", AboutSnippets.Length)],
            Interests[Pick($"{seed}-interests", Interests.Length)],
            Music[Pick($"{seed}-music", Music.Length)],
            MiningCompanyName(seed),
            TestingDummyFriendsPaths.AvatarUrl(index),
            TestingDummyFriendsPaths.BackgroundUrl(index),
            TestingDummyFriendsPaths.LogoUrl(index));
    }

    public static TestingDummyFriendsProfile? TryGet(int index)
    {
        if (index < 0 || index >= DummyCount)
        {
            return null;
        }

        return All().ElementAt(index);
    }

    public static IEnumerable<TestingDummyFriendsProfile> All() =>
        Enumerable.Range(0, DummyCount).Select(Get);

    public static Guid DummyFriendshipId(int index)
    {
        if (index < 0 || index >= DummyCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Guid.Parse($"{DummyFriendshipIdPrefix}{index:D12}");
    }

    public static bool IsValidIndex(int index) => index >= 0 && index < DummyCount;

    private static string DummyProfileNumber(int index) =>
        (100_000 + index * 7919).ToString(CultureInfo.InvariantCulture)[..6];

    private static Guid DummyPlayerId(int index) =>
        Guid.Parse($"{DummyPlayerIdPrefix}{index:D12}");

    private static string MiningCompanyName(string seed)
    {
        var prefix = MinePrefixes[Pick($"{seed}-p", MinePrefixes.Length)];
        var core = MineCores[Pick($"{seed}-c", MineCores.Length)];
        var suffix = MineSuffixes[Pick($"{seed}-s", MineSuffixes.Length)];
        var number = 100 + Pick($"{seed}-n", 8900);
        return $"{prefix} {core} {suffix} {number}";
    }

    private static int Pick(string seed, int maxExclusive) =>
        (int)(HashSeed(seed) % (uint)maxExclusive);

    private static uint HashSeed(string seed)
    {
        uint hash = 0;
        foreach (var ch in seed)
        {
            hash = hash * 31 + ch;
        }

        return hash;
    }
}
