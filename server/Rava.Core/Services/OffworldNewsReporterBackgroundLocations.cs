namespace Rava.Core.Services;

/// <summary>
/// Distinct ONN field locations for AI-generated reporter profile banner backgrounds.
/// Each scene reflects where the correspondent files or embeds—not a generic office.
/// </summary>
public static class OffworldNewsReporterBackgroundLocations
{
    public static string DescribeScene(OffworldNewsReporterProfile reporter)
    {
        if (KnownScenes.TryGetValue(reporter.Slug, out var scene))
        {
            return scene;
        }

        return BuildFallbackScene(reporter);
    }

    /// <summary>Reader-facing note for reporter profiles listing signature embed locations.</summary>
    public static string ProfileNote(OffworldNewsReporterProfile reporter)
    {
        if (ProfileNotes.TryGetValue(reporter.Slug, out var note))
        {
            return note;
        }

        return
            $"Noteworthy embeds across {reporter.Bureau.Trim()} and other {reporter.Beat.Trim().ToLowerInvariant()} beats on the belt relay.";
    }

    private static string BuildFallbackScene(OffworldNewsReporterProfile reporter)
    {
        var specialty = reporter.Specialties.FirstOrDefault() ?? reporter.Beat;
        return
            $"signature ONN filing spot at {reporter.Bureau.Trim()}, " +
            $"view and props tied to {reporter.Beat.Trim()} coverage and {specialty}, " +
            "empty press workstation and equipment ready for a live dispatch";
    }

    private static readonly Dictionary<string, string> KnownScenes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mira-solano"] =
                "Ceres Relay ore-spread gallery overlooking belt terminals, " +
                "wall of Rax and refinery bid tickers, NPC spread comparison monitors, empty markets desk before the open",

            ["jonah-kest"] =
                "Belt Sector 7 mining drift mouth where claim disputes break, " +
                "rock dust on survey stakes, safety lamp strings, extraction equipment and claim marker lights, no crew present",

            ["priya-menon"] =
                "Luna Port shipping dispatch tower with refinery queue boards, " +
                "holographic cargo manifests, dispatch window countdown clocks, hauler docking bays through panoramic windows",

            ["cassian-holt"] =
                "Phobos Anchorage syndicate filing atrium, " +
                "company-name auction screens, Trade Market transfer receipt wall, corporate rebranding archive terminals",

            ["elena-varga"] =
                "Vesta Charter Station Orbital Commons press gallery, " +
                "charter vote tally boards, registry clerk desks, diplomatic chamber doors and policy docket shelves",

            ["marcus-whitaker"] =
                "Titan Freight Hub pre-dawn wire room, " +
                "morning market wrap ticker wall, emergency buyback alert panel, auction terminal row glowing in blue dark",

            ["sable-nguyen"] =
                "Callisto Outer Rim Exonet relay nexus, " +
                "patch-note terminals, profile upload diagnostic racks, interplanetary relay log screens and outage status boards",

            ["theo-brassard"] =
                "Europa Deep Survey outer-rim camp ridge lookout, " +
                "unpublished coordinate charts on a field table, ore sample trays, wide viewport to an unseen survey planet horizon",

            ["ingrid-falk"] =
                "Ceres Relay pilot briefing overlook above outer freight lanes, " +
                "fuel-cell gauge wall, life-support spike monitors, lane-status map and broker queue displays",

            ["devon-ashcroft"] =
                "Luna Port ONN column desk facing company-value leaderboard projections, " +
                "payroll pressure dashboards, syndicate gossip bulletin screens, late-night newsroom glow",

            ["lena-okonkwo"] =
                "Phobos Anchorage charter policy archive hall, " +
                "RAVA production-rule timeline wall, vote-count clocks, NPC refinery policy binders and transparency docket shelves",

            ["rafael-cruz"] =
                "Vesta Charter mine operations catwalk above active zones, " +
                "worker assignment boards, zone output dashboards, ore-in-cargo hold monitors and belt vent stacks",

            ["yumiko-ito"] =
                "Callisto Outer Rim supply auction floor, " +
                "Comm Module bid heat maps, drill-bit auction staging crates, live stock-symbol boards and relay fault indicators",

            ["anders-lindqvist"] =
                "Belt Sector 7 bureau chief window overlooking jammed refinery doors, " +
                "Salvage Scrap cargo yard, route competition map, muted rival Ceres headline feed on a side monitor",

            ["zara-pemberton"] =
                "Titan Freight Hub digital frontier studio, " +
                "Exonet trade-page mockups on curved screens, upload-queue monitor wall, auction-fee pool ticker and market-value charts",
        };

    private static readonly Dictionary<string, string> ProfileNotes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mira-solano"] =
                "Noteworthy embeds: Ceres Relay ore-spread gallery, belt terminal refinery bids, Luna Port NPC spread desks.",

            ["jonah-kest"] =
                "Noteworthy embeds: Belt Sector 7 mining drifts, active claim markers, night-shift extraction zones.",

            ["priya-menon"] =
                "Noteworthy embeds: Luna Port dispatch tower, refinery queue windows, hauler manifest bays.",

            ["cassian-holt"] =
                "Noteworthy embeds: Phobos Anchorage syndicate filing hall, company-name auction floor, Trade Market transfer archive.",

            ["elena-varga"] =
                "Noteworthy embeds: Vesta Charter Orbital Commons gallery, charter vote halls, registry clerk desks.",

            ["marcus-whitaker"] =
                "Noteworthy embeds: Titan Freight Hub morning wire room, emergency buyback boards, live auction terminals.",

            ["sable-nguyen"] =
                "Noteworthy embeds: Callisto Outer Rim Exonet relay nexus, profile upload diagnostics, interplanetary patch rollout sites.",

            ["theo-brassard"] =
                "Noteworthy embeds: Europa Deep Survey outer-rim camps, unpublished ridge coordinates, scout ore-mix sample sites.",

            ["ingrid-falk"] =
                "Noteworthy embeds: Ceres Relay pilot briefing decks, outer freight lanes, fuel-cell and life-support spike zones.",

            ["devon-ashcroft"] =
                "Noteworthy embeds: Luna Port ONN column desk, company-value leaderboard pits, midnight payroll pressure beats.",

            ["lena-okonkwo"] =
                "Noteworthy embeds: Phobos Anchorage policy archive, charter vote clocks, RAVA production-rule briefing rooms.",

            ["rafael-cruz"] =
                "Noteworthy embeds: Vesta Charter mine operations catwalks, worker zone floors, ore-in-cargo hold monitors.",

            ["yumiko-ito"] =
                "Noteworthy embeds: Callisto supply auction floor, Comm Module bid pits, drill-bit staging yards.",

            ["anders-lindqvist"] =
                "Noteworthy embeds: Belt Sector 7 refinery doors, Salvage Scrap cargo yards, Ceres route competition beats.",

            ["zara-pemberton"] =
                "Noteworthy embeds: Titan Freight Hub digital frontier studio, Exonet trade mockups, upload-queue war rooms.",
        };
}
