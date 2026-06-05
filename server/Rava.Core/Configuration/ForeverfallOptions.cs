namespace Rava.Core.Configuration;

public class ForeverfallOptions
{
    public const string SectionName = "Foreverfall";

    public bool Enabled { get; set; } = true;

    /// <summary>Relative to the Exonet root. Stores rosters and portrait pool.</summary>
    public string CacheDirectory { get; set; } = "foreverfall";

    public int MaxInmateImages { get; set; } = 500;

    public int RetentionDays { get; set; } = 14;

    public int TargetDailyIntake { get; set; } = 15;

    public int IntakeVariance { get; set; } = 8;

    public int MinDailyIntake { get; set; } = 7;

    public int MaxDailyIntake { get; set; } = 23;
}
