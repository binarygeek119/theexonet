using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using System.Globalization;

namespace Theexonet.Infrastructure.Services;

/// <summary>
/// Synthetic friend profiles for admin testing mode. IDs and usernames must stay in sync with
/// html/js/admin-testing-mode.js.
/// </summary>
public static class TestingDummyFriends
{
    private const int DummyCount = 12;
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

    public static IReadOnlyList<FriendSummaryDto> MergeFriendSummaries(IReadOnlyList<FriendSummaryDto> friends)
    {
        var seen = friends.Select(f => f.PlayerId).ToHashSet();
        var merged = friends.ToList();

        foreach (var dummy in BuildFriendSummaries())
        {
            if (seen.Add(dummy.PlayerId))
            {
                merged.Add(dummy);
            }
        }

        return merged
            .OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ProfileFriendDto> MergeProfileFriends(IReadOnlyList<ProfileFriendDto> friends)
    {
        var seen = friends.Select(f => f.PlayerId).ToHashSet();
        var merged = friends.ToList();

        foreach (var dummy in BuildProfileFriends())
        {
            if (seen.Add(dummy.PlayerId))
            {
                merged.Add(dummy);
            }
        }

        return merged
            .OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<FriendSummaryDto> BuildFriendSummaries()
    {
        for (var index = 0; index < DummyCount; index++)
        {
            var seed = $"dummy-profile-{index}";
            var daysFriends = 1 + Pick($"{seed}-friends", 90);

            yield return new FriendSummaryDto(
                DummyFriendshipId(index),
                DummyPlayerId(index),
                Usernames[index],
                DummyProfileNumber(index),
                Moods[Pick($"{seed}-mood", Moods.Length)],
                FriendshipStatuses.Accepted,
                DateTime.UtcNow.AddDays(-daysFriends),
                IsTestingDummy: true);
        }
    }

    private static IEnumerable<ProfileFriendDto> BuildProfileFriends()
    {
        for (var index = 0; index < DummyCount; index++)
        {
            var seed = $"dummy-profile-{index}";

            yield return new ProfileFriendDto(
                DummyPlayerId(index),
                Usernames[index],
                DummyProfileNumber(index),
                Moods[Pick($"{seed}-mood", Moods.Length)],
                string.Empty,
                IsTestingDummy: true);
        }
    }

    private static string DummyProfileNumber(int index) =>
        (100_000 + index * 7919).ToString(CultureInfo.InvariantCulture)[..6];

    private static Guid DummyPlayerId(int index) =>
        Guid.Parse($"{DummyPlayerIdPrefix}{index:D12}");

    private static Guid DummyFriendshipId(int index) =>
        Guid.Parse($"{DummyFriendshipIdPrefix}{index:D12}");

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
