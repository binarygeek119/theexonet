namespace Rava.Core.Constants;

/// <summary>
/// Credit grant amounts for bonuses and starting balances.
/// Defaults live here; override at runtime via server/Rava.Api/credits.csv.
/// </summary>
public static class GameCredits
{
    /// <summary>Credits granted when a new player signs up.</summary>
    public const decimal SignUp = 5000m;

    /// <summary>Bonus credits on the player's birthday (once per calendar year).</summary>
    public const decimal BirthdayBonus = 500m;

    /// <summary>Fee to reclaim a company name the player gave up within the 30-day limbo window.</summary>
    public const decimal CompanyNameReclaimFee = 2500m;
}
