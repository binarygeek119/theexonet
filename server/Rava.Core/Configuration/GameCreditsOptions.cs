using Rava.Core.Constants;

namespace Rava.Core.Configuration;

public class GameCreditsOptions
{
    public const string SectionName = "Credits";

    /// <summary>Credits granted when a new player signs up.</summary>
    public decimal SignUp { get; set; } = GameCredits.SignUp;

    /// <summary>Bonus credits on the player's birthday (once per calendar year).</summary>
    public decimal BirthdayBonus { get; set; } = GameCredits.BirthdayBonus;
}
