using Theexonet.Core.Configuration;

namespace Theexonet.Core.Services;

public static class ForeverfallInmateTemplateGenerator
{
    private static readonly string[] MaleFirstNames =
    [
        "Marcus", "Derek", "Kael", "Torin", "Jax", "Rhen", "Viktor", "Soren", "Cade", "Orin",
    ];

    private static readonly string[] FemaleFirstNames =
    [
        "Lyra", "Mira", "Selene", "Tessa", "Nadia", "Kira", "Elara", "Vera", "Sana", "Yuna",
    ];

    private static readonly string[] LastNames =
    [
        "Voss", "Keth", "Marrek", "Solano", "Thane", "Ivers", "Quell", "Dravik", "Chen", "Okonkwo",
    ];

    private static readonly string[] Species =
    [
        "Human", "Human", "Centauri", "Vulpine", "Silicate", "Arachnid", "Aquatic", "Crystalline",
    ];

    private static readonly string[] Crimes =
    [
        "Unauthorized warp core trafficking",
        "Belt claim-jumping with armed escort",
        "Antimatter smuggling through neutral lanes",
        "First-contact treaty violation",
        "Hijacking of ore convoy AX-441",
        "Falsifying jump clearance manifests",
        "Corporate espionage against VoidCorp subsidiaries",
        "Sabotage of relay station life support",
    ];

    public static IReadOnlyList<GeneratedForeverfallInmate> Generate(
        DateOnly intakeDate,
        int count,
        int maleCount,
        int femaleCount)
    {
        var rng = new Random(HashCode.Combine(intakeDate.DayNumber, 0xFF33));
        var results = new List<GeneratedForeverfallInmate>(count);
        var malesLeft = maleCount;
        var femalesLeft = femaleCount;

        for (var index = 0; index < count; index++)
        {
            var gender = malesLeft > 0 && (femalesLeft == 0 || rng.NextDouble() >= 0.5)
                ? "male"
                : "female";
            if (gender == "male")
            {
                malesLeft--;
            }
            else
            {
                femalesLeft--;
            }

            var first = gender == "male"
                ? MaleFirstNames[rng.Next(MaleFirstNames.Length)]
                : FemaleFirstNames[rng.Next(FemaleFirstNames.Length)];
            var last = LastNames[rng.Next(LastNames.Length)];
            var species = Species[rng.Next(Species.Length)];
            var crime = Crimes[rng.Next(Crimes.Length)];

            results.Add(new GeneratedForeverfallInmate(
                $"{first} {last}",
                species,
                gender,
                crime,
                "Galactic lifetime — remanded to Foreverfall event-horizon labor until lawful heat-death review.",
                $"Transferred from frontier tribunal docket {intakeDate:yyyyMMdd}-{index + 1:D3} after conviction.",
                $"{first} {last} ({species}) was convicted of {crime.ToLowerInvariant()}. " +
                $"Foreverfall Penitentiary accepted custody on {intakeDate:yyyy-MM-dd} under maximum containment protocols."));
        }

        return results;
    }
}

public sealed record GeneratedForeverfallInmate(
    string DisplayName,
    string Species,
    string Gender,
    string Crime,
    string Sentence,
    string IntakeReason,
    string Bio);
