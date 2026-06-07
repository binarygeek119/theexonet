namespace Theexonet.Core.Services;

/// <summary>Required story types included in each Offworld News edition.</summary>
public static class OffworldNewsEditionStoryTypes
{
    public const string Politics = "Politics";
    public const string NewPlanets = "New Planets";
    public const string Work = "Work";
    public const string Stocks = "Stocks";
    public const string Companies = "Companies";

    private const int RotationSalt = 0x0FF1;

    public static readonly string[] Core =
    [
        Politics,
        NewPlanets,
        Work,
        Stocks,
        Companies,
    ];

    private static readonly string[] Supplemental =
    [
        "Security",
        "Shipping",
        "Exonet",
    ];

    public static IReadOnlyList<string> TypesForEdition(DateOnly editionDate, int storyCount)
    {
        storyCount = Math.Max(0, storyCount);
        if (storyCount == 0)
        {
            return [];
        }

        if (storyCount <= Core.Length)
        {
            return Core.Take(storyCount).ToList();
        }

        var types = new List<string>(Core);
        var rng = new Random(HashCode.Combine(editionDate.DayNumber, RotationSalt));
        for (var index = Core.Length; index < storyCount; index++)
        {
            types.Add(Supplemental[rng.Next(Supplemental.Length)]);
        }

        return types;
    }

    public static string PlaceholderSlug(string storyType) =>
        storyType.Trim() switch
        {
            Politics => "politics",
            NewPlanets => "frontier",
            Work => "mining",
            Stocks => "markets",
            Companies => "corporate",
            "Security" => "security",
            "Shipping" => "shipping",
            "Exonet" => "exonet",
            "Markets" => "markets",
            "Mining" => "mining",
            "Corporate" => "corporate",
            "Frontier" => "frontier",
            _ => "markets",
        };

    public static string PlaceholderImageUrl(string storyType) =>
        $"/exonet/offworld-news/placeholders/{PlaceholderSlug(storyType)}.svg";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Stocks;
        }

        var trimmed = value.Trim();
        return trimmed switch
        {
            Politics or "politics" => Politics,
            NewPlanets or "New Planet" or "new planets" or "Frontier" or "frontier" => NewPlanets,
            Work or "work stuff" or "Work Stuff" or "Mining" or "mining" => Work,
            Stocks or "stocks" or "Markets" or "markets" => Stocks,
            Companies or "companys" or "companies" or "Corporate" or "corporate" => Companies,
            "Security" or "security" => "Security",
            "Shipping" or "shipping" => "Shipping",
            "Exonet" or "exonet" => "Exonet",
            _ when Core.Contains(trimmed, StringComparer.OrdinalIgnoreCase) => Core.First(
                type => type.Equals(trimmed, StringComparison.OrdinalIgnoreCase)),
            _ => trimmed,
        };
    }

    public static string PromptDescription(string storyType) =>
        storyType switch
        {
            Politics =>
                "interplanetary politics — Orbital Commons votes, charter hearings, registry rules, diplomacy, ceasefire talks",
            NewPlanets =>
                "new planets and discovery — surveys, watchlist worlds, traveler advisories, charter fold votes, first-contact ethics",
            Work =>
                "work on the frontier — mining crews, refinery queues, payroll pressure, worker assignments, shipping labor, drill-bit and life-support consumption",
            Stocks =>
                "belt stocks and markets — Rax liquidity, ore spreads, supply stock bids, Trade Market volume, emergency buy backs",
            Companies =>
                "mining companies — player-style firms rising or struggling, corporate filings, company value rankings, syndicate deals",
            "Security" =>
                "security and crime — marshals, smugglers, black routes, bounty postings, border skirmishes",
            "Shipping" =>
                "shipping and cargo — hauler backlogs, manifest inspections, refinery dispatch windows, lane diversions",
            "Exonet" =>
                "Exonet and the public web — leaderboards, miner profiles, relay traffic, directory lookups",
            _ => "general frontier news",
        };

}
