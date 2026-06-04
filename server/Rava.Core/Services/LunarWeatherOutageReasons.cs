namespace Rava.Core.Services;

/// <summary>Reasons a weather relay failed to upload a midnight bulletin.</summary>
public static class LunarWeatherOutageReasons
{
    public static readonly IReadOnlyList<string> All =
    [
        "Power outage",
        "Satellite linkage blockage",
        "Terrorist interference with relay uplink",
        "Political embargo on telemetry export",
        "Relay processor overheated",
        "Telemetry buffer overloaded",
        "Security certification failed",
        "Primary weather sensor failure",
        "Secondary weather sensor failure",
        "Weather equipment failure",
        "Weather equipment issues",
        "Weather equipment uncalibrated",
        "Weather equipment connection lost",
        "Particle counter drift — recalibration required",
        "Radiation dosimeter uncalibrated",
        "Magnetometer connection lost",
        "Plasma sheath monitor offline",
        "Dust electrostatic array fault",
        "Solar wind gauge jammed",
        "Micrometeor shield breach — safe mode",
        "Cryocooler pump failure",
        "Antenna gimbal locked",
        "Orbital debris strike — structural inspection",
        "Firmware rollback in progress",
        "Quantum clock desync",
        "Ground station handoff timeout",
        "Bandwidth rationed — civilian priority suspended",
        "Crew evacuation — relay unmanned",
        "Docking collision — mast alignment lost",
        "Comet tail interference",
        "Flare warning — instruments stowed",
        "Registry audit — transmission hold",
        "Insurance inspection — data export frozen",
        "Vendor maintenance window overrun",
        "Cryogenic seal leak — compartment venting",
        "Relay coprocessor fault",
        "Star tracker blinded by nebula glare",
        "Laser comm array misaligned",
        "Hydrogen feed line frozen",
    ];

    public static string PickForRelay(DateOnly date, string relayId, int index)
    {
        if (All.Count == 0)
        {
            return "Unknown telemetry fault";
        }

        var hash = HashCode.Combine(date.DayNumber, relayId, index, 0x4C57);
        return All[Math.Abs(hash) % All.Count];
    }

    public static string? PickDetail(DateOnly date, string relayId, string issue)
    {
        var hash = HashCode.Combine(date.DayNumber, relayId, issue, 0xD371);
        var templates = new[]
        {
            "Automated retry scheduled after the next orbital window.",
            "Field team dispatched on the next available shuttle slot.",
            "No ETA until cross-link geometry improves.",
            "Backup capacitor bank charging — expect intermittent pings.",
            "Incident ticket LW-{0:D4} opened with Belt Operations.",
            "Upstream aggregator marked this node as stale.",
            "Manual override required from Lunar Weather Service HQ.",
            "Neighboring relay cross-check also degraded.",
        };

        var ticket = Math.Abs(HashCode.Combine(relayId, date.DayNumber)) % 9000 + 1000;
        return string.Format(templates[Math.Abs(hash) % templates.Length], ticket);
    }
}
