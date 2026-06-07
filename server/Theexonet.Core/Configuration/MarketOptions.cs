namespace Theexonet.Core.Configuration;

public class MarketOptions
{
    public const string SectionName = "Market";

    public bool UseLiveData { get; set; } = true;

    /// <summary>
    /// CSV spreadsheet filename in the API content root (opens in Excel).
    /// </summary>
    public string ItemsFile { get; set; } = "market-items.csv";
}
