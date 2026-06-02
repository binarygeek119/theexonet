using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Rava.Core.Dtos;

namespace Rava.Core.Services;

public static partial class OffworldNewsTemplateGenerator
{
    private static readonly string[] Categories =
    [
        "Markets",
        "Mining",
        "Corporate",
        "Shipping",
        "Politics",
        "Exonet",
        "Frontier",
        "Security",
    ];

    private static readonly string[] Locations =
    [
        "Ceres Relay",
        "Luna Port",
        "Belt Sector 7",
        "Phobos Anchorage",
        "Titan Freight Hub",
        "Europa Deep Survey",
        "Vesta Charter Station",
        "Callisto Outer Rim",
        "Survey Planet K-19",
        "The Meridian Rift",
        "Halo-7 Watch Arc",
        "Driftward Expanse",
    ];

    private static readonly string[] FakeCompanyNames =
    [
        "VoidCorp Holdings",
        "Redshift Logistics",
        "Ceres Consolidated Mining",
        "Helios Belt Freight",
        "Titan Ore Syndicate",
        "Phobos Deep Works",
        "Luna Trade Collective",
        "Sagittarius Survey Group",
        "Orbital Commons Alliance",
        "Nebula Refinery Partners",
        "Kuiper Lane Shipping",
        "Iron Halo Extraction",
    ];

    public static OffworldNewsEditionDto Generate(
        DateOnly editionDate,
        int storyCount,
        OffworldNewsCompanyContext? companyContext = null)
    {
        storyCount = Math.Clamp(storyCount, 1, 10);
        var random = CreateRandom(editionDate);
        var publishedBase = editionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(6);
        var rising = companyContext?.RisingCompanies ?? [];
        var struggling = companyContext?.StrugglingCompanies ?? [];

        var stories = new List<OffworldNewsStoryDto>();
        for (var index = 0; index < storyCount; index++)
        {
            var template = StoryTemplates[index % StoryTemplates.Length];
            var category = Categories[index % Categories.Length];
            var companyName = PickCompany(random, rising, struggling, index);
            var topic = Pick(random, TopicSubjects);
            var actor = Pick(random, BodyActors);
            var detail = Pick(random, BodyDetails);
            var location = Pick(random, Locations);
            var headline = string.Format(template.Headline, companyName, topic, actor, location);
            var dek = string.Format(template.Dek, companyName, topic, actor, location);
            var reporter = OffworldNewsReporterCatalog.PickForStory(editionDate, index);
            var body = string.Format(template.Body, companyName, actor, detail, location);
            if (!string.IsNullOrWhiteSpace(reporter.StoryKicker))
            {
                body = $"{body}\n\n{reporter.StoryKicker}";
            }

            stories.Add(new OffworldNewsStoryDto(
                $"{Slugify(headline)}-{index + 1}",
                headline,
                dek,
                body,
                category,
                location,
                reporter.DisplayName,
                reporter.Slug,
                publishedBase.AddHours(index * 2.5),
                companyName,
                PlaceholderImageForCategory(category)));
        }

        return new OffworldNewsEditionDto(
            editionDate,
            DateTime.UtcNow,
            "template",
            stories);
    }

    private static string PickCompany(
        Random random,
        IReadOnlyList<string> rising,
        IReadOnlyList<string> struggling,
        int index)
    {
        if (index % 5 == 0 && rising.Count > 0)
        {
            return rising[random.Next(rising.Count)];
        }

        if (index % 5 == 2 && struggling.Count > 0)
        {
            return struggling[random.Next(struggling.Count)];
        }

        return FakeCompanyNames[random.Next(FakeCompanyNames.Length)];
    }

    public static string PlaceholderImageForCategory(string category) =>
        $"/exonet/offworld-news/placeholders/{SlugifyCategory(category)}.svg";

    private static string SlugifyCategory(string category) =>
        category.ToLowerInvariant() switch
        {
            "markets" => "markets",
            "mining" => "mining",
            "corporate" => "corporate",
            "shipping" => "shipping",
            "politics" => "politics",
            "exonet" => "exonet",
            "frontier" => "frontier",
            "security" => "security",
            _ => "markets",
        };

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

    private static readonly string[] TopicSubjects =
    [
        "Ferroxite",
        "Voidium",
        "Stellarite",
        "Salvage Scrap",
        "drill-bit futures",
        "Exonet relay bandwidth",
        "outer-belt shipping lanes",
        "Rax liquidity pools",
        "emergency buy back volume",
        "company-name auctions",
        "a newly charted exoplanet",
        "a pre-contact watchlist world",
        "an outer-rim charter vote",
        "a border skirmish corridor",
        "a smuggler black-route",
    ];

    private static readonly string[] BodyActors =
    [
        "Reactive Asteroid Venturing Agency observers",
        "NPC refinery buyers",
        "public Exonet directories",
        "orbital cargo inspectors",
        "Rax clearinghouses",
        "independent belt surveyors",
        "Trade Market auction clerks",
        "Outer Rim Patrol cutters",
        "Interplanetary Marshal Service agents",
        "Orbital Commons peace envoys",
        "deep-survey probe teams",
        "black-route smuggling syndicates",
    ];

    private static readonly string[] BodyDetails =
    [
        "a delayed ore convoy",
        "fresh assay rumors from Sector 7",
        "tighter supply consumption on game-day rollovers",
        "new charter filings for outer-planet claims",
        "political pressure from the Orbital Commons",
        "record drill-bit burn rates",
        "a long-range sensor sweep under non-interference doctrine",
        "navigation beacons for licensed hauler lanes",
        "a ceasefire line contested by rival flotillas",
        "bounty postings on interplanetary fugitives",
        "first-contact briefings for independent captains",
    ];

    private static readonly StoryTemplate[] StoryTemplates =
    [
        new(
            "{1} rally lifts {0} after stellar refinery bids",
            "Live US-linked supply stocks and ore spreads moved before the UTC midnight edition.",
            "{0} led the morning belt markets after {1} confirmed unusually tight Ferroxite and Voidium spreads on public terminals. Traders on Ceres Relay said {0} crews were holding cargo until NPC buyers posted clearer Rax prices.\n\nAnalysts cautioned that emergency buy back chatter could still cap upside if payroll pressure rises before the next game day. {2} noted that auction fee pools on the Trade Market also climbed overnight, feeding the shared market value figure on status dashboards."),
        new(
            "{0} warns of shipping backlog on {1} routes",
            "Cargo manifests must match in-game inventory before dispatch, inspectors repeat.",
            "Lines at NPC refinery doors lengthened after {0} redirected haulers away from {3}. {1} said several captains missed dispatch windows because ore still sat in mine cargo holds instead of ready manifests.\n\nThe Shipping Authority feed reminded operators that extracted ore must be in cargo before the shipping panel can move it. {2} reported that {0} is negotiating priority slots with {1} brokers while fuel-cell stocks run thin on outer lanes."),
        new(
            "Interplanetary council debates charter for {3}",
            "{0} and rival syndicates clash over who may open the next claim window.",
            "Delegates from the Orbital Commons Alliance opened hearings on whether {3} should host a new public claim lottery. {0} argued that established operators like itself deserve first survey rights, while {1} lobbyists pushed for open access to independent miners.\n\n{2} said the vote could reshape how new planets enter the public Exonet registry. Observers expect a compromise that keeps RAVA starter rules intact while allowing limited outer-rim pilots."),
        new(
            "{0} opens new mine face after {1} survey",
            "Fresh zones and worker assignments are expected to ripple through company value rankings.",
            "{0} announced a new extraction face after {1} completed a charter survey near {3}. Company registries on Exonet Miner Profiles logged a spike in lookups as investors guessed how ore stockpiles might change at base prices.\n\n{2} called the opening a modest boost for runway days if supplies stay above minimums. Rival {1} operators warned that depletion on older tiles still threatens payroll unless drill-bit orders keep pace."),
        new(
            "{0} faces payroll squeeze despite {1} headline",
            "Soft-locked crews may lean on 50% emergency buy backs before the next game day.",
            "Financial relay traffic shows {0} reviewing emergency buy back rates on the Shipping Authority feed after {2} flagged rising life-support consumption. {1} spreads offered little relief as NPC refineries held firm on Salvage Scrap bids.\n\nFormer allies in the belt exchange said {0} may list branded company names on the Trade Market if Rax balances do not recover. {2} emphasized that public rankings are rough proxies, not formal audits."),
        new(
            "Exonet leaderboard reshuffled as {0} climbs",
            "Public company value rankings drew heavy traffic overnight on the interplanetary browser.",
            "The Exonet directory logged a surge in profile lookups after revised company value estimates moved {0} into the top tier. The metric blends Rax, ore at base prices, supplies, and equipment, making it a favorite gossip score rather than a formal filing.\n\n{1} analysts said {0} benefited from disciplined worker assignments and steady zone output. {2} reminded readers that miners in distress still appear on the same board, and today's winners can become tomorrow's cautionary tales."),
        new(
            "Trade Market heats up as {0} chases {1} listings",
            "Completed sales continue feeding the public Trade Market value tracker.",
            "Auction clerks report brisk bidding on supply bundles and recycled company names tied to {0}. Several listings originated from operators who relinquished mine branding during Rax crunches, then watched names re-enter the market after limbo periods.\n\n{1} said a portion of each completed sale still flows into the shared market value figure displayed on Exonet trade pages. {2} urged buyers to verify listing prices before purchasing a name from the player trade store."),
        new(
            "New planet survey filed for {3} outer rim",
            "{0} sponsors probe as {1} traffic lights up charter offices.",
            "A joint filing from {0} and {1} survey contractors proposed a staged opening for new claim tiles beyond Belt Sector 7. RAVA agency staff said any production rule changes would ship through normal game updates, not overnight decree.\n\n{2} reported strong Exonet chatter about which ore mixes the region might favor. Politicians in the Orbital Commons demanded transparency on how starter mines and NPC refineries would interact with the new lanes."),
        new(
            "{0} comm modules spike on {1} outage rumors",
            "Exonet relay traffic rerouted through backup nodes for six hours.",
            "Comm-module supply bids jumped after a partial relay fault forced {0} traffic through backup nodes. {1} sellers on the Trade Market posted premiums while independent miners complained about sluggish profile uploads.\n\n{2} said RAVA production systems stayed online throughout the incident. {0} pledged to restock Comm Modules at Luna Port if auction prices remain elevated through the UTC day boundary."),
        new(
            "Corporate registry flags unusual {0} filings",
            "{1} names and mine transfers draw regulator side-eye on Exonet.",
            "Corporate monitors flagged a burst of {0} activity around mine transfers and company-name listings. {1} investigators said some filings coincided with emergency buy back spikes, suggesting operators were recycling brands to raise quick Rax.\n\n{2} noted that relinquished names remain reserved for thirty days before re-entering public use. Market desks said {0} remains solvent for now, but interplanetary politics may tighten disclosure rules if the pattern continues."),
        new(
            "Watchlist probe maps {3} under observation-only charter",
            "{0} keeps distance as RAVA non-interference rules bar surface contact.",
            "Deep-survey telemetry from {0} placed {3} on the agency watchlist after spectroscopy hinted at habitable bands and unusual ore signatures. Under observation-only rules, no landing parties, trade windows, or claim filings may proceed until the Orbital Commons finishes a multi-cycle review.\n\n{2} compared the posture to classic first-contact doctrine: look, record, do not steer. {1} lobbyists already argue the world should stay quarantined forever, while {0} sponsors quietly fund passive sensor arrays in the {3} halo."),
        new(
            "Traveler advisory opens {3} to licensed outer-rim convoys",
            "{0} publishes beacon charts as {1} captains gain clearance to pass through.",
            "After three observation cycles, charter clerks upgraded {3} from watchlist-only to traveler-introduced status. Licensed haulers may now download navigation beacons and receive diplomatic briefings at {0} relay desks, though mining claims and permanent bases remain forbidden.\n\n{2} said the step mirrors how earlier belt worlds entered the public lane network: introduced carefully, exploited later. Captains on Exonet trade pages debated whether {1} routes through the region justify extra fuel-cell reserves before the next game day."),
        new(
            "{3} votes to join the outer-rim charter fold",
            "{0} celebrates as new world enters the shared journey with claim windows ahead.",
            "Delegates ratified {3} as a full charter participant, moving the world from traveler access to active membership in the belt relay community. {0} pledged survey teams and starter-compatible claim templates so independent miners can eventually open faces under RAVA production rules.\n\n{2} called it the kind of milestone that reshapes Exonet maps overnight. {1} analysts warned that ore mixes on the new world could shift NPC refinery bids once the first cargo manifests clear inspection at {0}."),
        new(
            "Border flotillas clash near {3} as interplanetary war fears rise",
            "{0} convoys diverted while Orbital Commons envoys chase a ceasefire line.",
            "Patrol cutters from rival syndicates exchanged warning shots across a contested corridor near {3}, forcing {0} haulers onto longer {1} detours. {2} reported damaged comm modules on two freighters and a twelve-hour freeze on neutral shipping manifests.\n\nOrbital Commons peace envoys opened an emergency channel, invoking belt-neutrality precedes from earlier charter wars. Market desks said {1} spreads widened anyway, and {0} crews stocked extra life support before the UTC midnight edition."),
        new(
            "Interplanetary marshals bust {1} ring tied to {0}",
            "Black-route smugglers accused of Rax laundering across {3}.",
            "The Interplanetary Marshal Service unsealed indictments against a {1} cartel accused of running stolen ore and falsified cargo tags through {3}. Investigators said {0} appeared on wire chatter as a fence for hot Ferroxite and Voidium before refinery assays caught mismatched manifests.\n\n{2} published bounty notices on Exonet security feeds, warning captains not to accept rush charters from unknown brokers. Corporate monitors added that {0} itself is not charged, but syndicate lawyers demanded retractions before the next charter session."),
    ];
}
