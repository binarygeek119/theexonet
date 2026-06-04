using System.Security.Cryptography;
using System.Text;
using Rava.Core.Configuration;
using Rava.Core.Dtos;

namespace Rava.Core.Services;

public static class LunarWeatherTemplateGenerator
{
    private static readonly string[] AlertLevels = ["nominal", "caution", "advisory", "warning", "severe"];

    private static readonly string[] Conditions =
    [
        "Micrometeor flux elevated",
        "Solar wind spike",
        "Magnetopause shear",
        "Regolith static discharge risk",
        "Plasma sheath turbulence",
        "Radiation bloom from flare corridor",
        "Vacuum frost deposition",
        "Dust electrostatic arc hazard",
        "Coma outgassing plume drift",
        "Orbital debris flux watch",
        "Cryogenic vent crosswind",
        "Ion storm fringe",
        "Thermal swing across terminator",
        "Ablation haze in upper regolith",
        "Neutron background uptick",
    ];

    private static readonly string[] ParticleFluxNotes =
    [
        "Low micrometeor flux",
        "Moderate sporadic flux",
        "Elevated grain bombardment",
        "Heavy micrometeor stream",
        "Flux within survey tolerance",
    ];

    private static readonly string[] RadiationNotes =
    [
        "Background nominal for belt ops",
        "Elevated particle dose — suit check advised",
        "Radiation index above comfort band",
        "Flare corridor influence — shielded routes only",
        "Dosimeter green across bands",
    ];

    private static readonly string[] VisibilityNotes =
    [
        "Optical sensors clear in vacuum",
        "Haze layer reducing LIDAR range",
        "Dust lane — navigation lights recommended",
        "Star field occlusion from plume",
        "Long-range optics nominal",
    ];

    private static readonly string[] PressureNotes =
    [
        "Hard vacuum — no barometric regime",
        "Exosphere scrape on leading edge",
        "Outgassing pocket near sensor mast",
        "Pressure trace N/A — surface vacuum lock",
        "Thin exosphere ribbon at limb",
    ];

    public static LunarWeatherBulletinDto Generate(
        DateOnly bulletinDate,
        LunarWeatherOptions options,
        IReadOnlyList<LunarWeatherRelayProfile> operationalRelays,
        IReadOnlyList<LunarWeatherRelayProfile> outageRelays,
        int targetOperationalCount)
    {
        var random = CreateRandom(bulletinDate);
        var observedBase = bulletinDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddMinutes(5);
        var readings = new List<LunarWeatherReadingDto>();

        for (var index = 0; index < operationalRelays.Count; index++)
        {
            var relay = operationalRelays[index];
            var alert = AlertLevels[random.Next(AlertLevels.Length)];
            var conditionCount = random.Next(2, 5);
            var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (picked.Count < conditionCount)
            {
                picked.Add(Conditions[random.Next(Conditions.Length)]);
            }

            readings.Add(new LunarWeatherReadingDto(
                relay.Id,
                relay.Slug,
                relay.Name,
                relay.Region,
                relay.Sector,
                BuildSummary(random, relay, alert, picked),
                alert,
                picked.ToList(),
                ParticleFluxNotes[random.Next(ParticleFluxNotes.Length)],
                RadiationNotes[random.Next(RadiationNotes.Length)],
                VisibilityNotes[random.Next(VisibilityNotes.Length)],
                PressureNotes[random.Next(PressureNotes.Length)],
                observedBase.AddMinutes(index * 2.3)));
        }

        var outages = BuildOutages(bulletinDate, outageRelays);

        return new LunarWeatherBulletinDto(
            bulletinDate,
            DateTime.UtcNow,
            "template",
            options.RelayPoolSize,
            targetOperationalCount,
            readings.Count,
            outages.Count,
            readings,
            outages);
    }

    public static IReadOnlyList<LunarWeatherOutageDto> BuildOutages(
        DateOnly bulletinDate,
        IReadOnlyList<LunarWeatherRelayProfile> outageRelays)
    {
        var outages = new List<LunarWeatherOutageDto>();
        for (var index = 0; index < outageRelays.Count; index++)
        {
            var relay = outageRelays[index];
            var issue = LunarWeatherOutageReasons.PickForRelay(bulletinDate, relay.Id, index);
            outages.Add(new LunarWeatherOutageDto(
                relay.Id,
                relay.Slug,
                relay.Name,
                relay.Region,
                issue,
                LunarWeatherOutageReasons.PickDetail(bulletinDate, relay.Id, issue)));
        }

        return outages;
    }

    private static string BuildSummary(
        Random random,
        LunarWeatherRelayProfile relay,
        string alertLevel,
        IReadOnlyCollection<string> conditions)
    {
        var lead = conditions.FirstOrDefault() ?? "Telemetry nominal";
        return alertLevel switch
        {
            "severe" =>
                $"{relay.Name}: {lead}; convoy holds advised across {relay.Sector}.",
            "warning" =>
                $"{relay.Name}: {lead}; EVA windows shortened near {relay.Region}.",
            "advisory" =>
                $"{relay.Name}: {lead}; monitor particle counters through local midnight.",
            "caution" =>
                $"{relay.Name}: {lead}; routine belt traffic may see minor delays.",
            _ =>
                $"{relay.Name}: {lead}; conditions within expected vacuum envelope.",
        };
    }

    private static Random CreateRandom(DateOnly bulletinDate)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"lunar-weather:{bulletinDate:yyyy-MM-dd}"));
        return new Random(BitConverter.ToInt32(bytes, 0));
    }
}
