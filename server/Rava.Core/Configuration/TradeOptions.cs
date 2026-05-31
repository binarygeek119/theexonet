namespace Rava.Core.Configuration;

public class TradeOptions
{
    public const string SectionName = "Trade";

    /// <summary>
    /// CSV spreadsheet for Trade Market items (buy supplies, sell ore). Opens in Excel.
    /// </summary>
    public string ItemsFile { get; set; } = "trade-items.csv";
}
