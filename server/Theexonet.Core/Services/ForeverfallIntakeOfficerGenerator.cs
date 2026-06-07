namespace Theexonet.Core.Services;

/// <summary>Date-seeded intake officer names for Foreverfall Penitentiary daily rosters.</summary>
public static class ForeverfallIntakeOfficerGenerator
{
    private const int SelectorSalt = 0xFF0F;

    private static readonly string[] Officers =
    [
        "Warden Jonas Kallander",
        "Intake Chief Lyra Venn",
        "Supervisor Marcus Holt",
        "Registry Officer Selene Ortiz",
        "Custody Lead Torin Ashe",
        "Processing Chief Nadia Corrick",
        "Intake Marshal Viktor Hale",
        "Dock Officer Elara Penn",
    ];

    public static string Resolve(DateOnly intakeDate)
    {
        var rng = new Random(HashCode.Combine(intakeDate.DayNumber, SelectorSalt));
        return Officers[rng.Next(Officers.Length)];
    }
}
