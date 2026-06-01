using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Rava.Core.Dtos;

namespace Rava.Core.Services;

public static partial class OffworldNewsTemplateGenerator
{
    private static readonly string[] Categories = ["Markets", "Mining", "Corporate", "Shipping", "Exonet"];
    private static readonly string[] Locations =
    [
        "Ceres Relay",
        "Luna Port",
        "Belt Sector 7",
        "Phobos Anchorage",
        "Titan Freight Hub",
    ];

    private static readonly string[] Authors =
    [
        "Mira Solano",
        "Jonah Kest",
        "ONN Wire Desk",
        "Relay Correspondent",
        "Frontier Bureau",
    ];

    public static OffworldNewsEditionDto Generate(DateOnly editionDate, int storyCount)
    {
        storyCount = Math.Clamp(storyCount, 1, 10);
        var random = CreateRandom(editionDate);
        var publishedBase = editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(6);

        var stories = new List<OffworldNewsStoryDto>();
        for (var index = 0; index < storyCount; index++)
        {
            var template = StoryTemplates[random.Next(StoryTemplates.Length)];
            var headline = string.Format(template.Headline, Pick(random, HeadlineSubjects));
            stories.Add(new OffworldNewsStoryDto(
                $"{Slugify(headline)}-{index + 1}",
                headline,
                string.Format(template.Dek, Pick(random, DekDetails)),
                string.Format(template.Body, Pick(random, BodyDetails), Pick(random, BodyDetails)),
                Categories[index % Categories.Length],
                Pick(random, Locations),
                Pick(random, Authors),
                publishedBase.AddHours(index * 2.5),
                null));
        }

        return new OffworldNewsEditionDto(
            editionDate,
            DateTime.UtcNow,
            "template",
            stories);
    }

    private static Random CreateRandom(DateOnly date)
    {
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"offworld-news:{date:yyyy-MM-dd}"));
        return new Random(BitConverter.ToInt32(seedBytes, 0));
    }

    private static string Pick(Random random, IReadOnlyList<string> values) =>
        values[random.Next(values.Count)];

    private static string Slugify(string value)
    {
        var slug = SlugRegex().Replace(value.ToLowerInvariant(), "-").Trim('-');
        return slug.Length > 48 ? slug[..48].Trim('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();

    private readonly record struct StoryTemplate(string Headline, string Dek, string Body);

    private static readonly string[] HeadlineSubjects =
    [
        "Ferroxite",
        "Voidium",
        "Stellarite",
        "Salvage Scrap",
        "Rax liquidity",
        "drill-bit shortages",
        "Exonet relay traffic",
        "company-name listings",
        "orbital shipping queues",
        "independent miners",
    ];

    private static readonly string[] DekDetails =
    [
        "independent operators",
        "refinery schedulers",
        "the Belt exchanges",
        "frontier payroll offices",
        "trade auction desks",
    ];

    private static readonly string[] BodyDetails =
    [
        "Reactive Asteroid Venturing Agency observers",
        "NPC refinery buyers",
        "public Exonet directories",
        "orbital cargo inspectors",
        "Rax clearinghouses",
    ];

    private static readonly StoryTemplate[] StoryTemplates =
    [
        new(
            "{0} futures spike as relay traders chase fresh assay rumors",
            "Market desks across {0} report unusually tight spreads before the UTC day rollover.",
            "Offworld News Network monitors say {0} spent the morning refreshing market terminals as {1} confirmed higher-than-expected demand from outer-belt contracts. Several independent mining companies told ONN they are holding ore in cargo until refinery bids stabilize."),
        new(
            "Emergency buy back chatter rises after {0} payroll squeeze",
            "Operators warn that soft-locked crews may lean on 50% refinery buy backs before the next game day.",
            "Financial relay traffic shows more miners reviewing emergency buy back rates on the Shipping Authority feed. {0} and {1} both noted that Rax balances are being stretched by supply consumption and daily payroll."),
        new(
            "Exonet Miner Profiles leaderboard reshuffled by {0} valuations",
            "Public company value rankings on the interplanetary browser drew heavy traffic overnight.",
            "The Exonet directory logged a surge in profile lookups after {0} published revised company value estimates that include ore stockpiles at base prices. {1} called the metric a rough proxy, not a formal audit."),
        new(
            "Trade Market auction fee pool climbs as {0} listings close hot",
            "Completed sales continue feeding the public Trade Market value tracker.",
            "Auction clerks report brisk bidding on supply bundles and branded company names. {0} said a portion of each completed sale still flows into the shared market value figure displayed on status dashboards and Exonet trade pages."),
        new(
            "Shipping Authority warns of {0} backlog at outer refineries",
            "Cargo manifests must match in-game inventory before dispatch, inspectors repeat.",
            "Lines at NPC refinery doors lengthened after {0} redirected haulers from a delayed ore convoy. {1} reminded operators that extracted ore must sit in cargo holds before shipping panels can dispatch it."),
        new(
            "Company Exchange sees quirky {0} names return from limbo",
            "Relinquished company names stay reserved for 30 days before re-entering public use.",
            "Corporate registries on Exonet show renewed interest in recycled mine names as {0} cleared several limbo entries. {1} cautioned buyers to verify listing prices before purchasing a name from the player trade store."),
        new(
            "VoidCorp placeholder site still offline, {0} memes spread anyway",
            "Exonet users joke that the corporate portal remains 'under reconstruction in orbit.'",
            "Even with VoidCorp Holdings still marked coming soon on Exonet, {0} circulated satirical earnings reports across miner chat relays. {1} noted the prank traffic did not affect live RAVA production systems."),
        new(
            "Independent crews celebrate record {0} extraction shift",
            "Worker morale posts dominate social directories after a clean day advance.",
            "Mining forums highlighted crews that assigned workers to rich zones and kept supplies above minimums. {0} credited drill-bit and life-support stocks for the efficiency bump, while {1} warned depletion still looms on overworked tiles."),
    ];
}
