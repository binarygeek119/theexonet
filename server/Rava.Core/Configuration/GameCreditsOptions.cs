using Rava.Core.Constants;

namespace Rava.Core.Configuration;

public class GameCreditsOptions
{
    public const string SectionName = "Credits";

    /// <summary>CSV spreadsheet filename in the API content root (opens in Excel).</summary>
    public string CreditsFile { get; set; } = "credits.csv";

    /// <summary>Credits granted when a new player signs up.</summary>
    public decimal SignUp { get; set; } = GameCredits.SignUp;

    /// <summary>Bonus credits on the player's birthday (once per calendar year).</summary>
    public decimal BirthdayBonus { get; set; } = GameCredits.BirthdayBonus;

    /// <summary>Fee to reclaim a relinquished company name within the limbo period.</summary>
    public decimal CompanyNameReclaimFee { get; set; } = GameCredits.CompanyNameReclaimFee;
}
