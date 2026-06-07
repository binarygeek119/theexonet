using Theexonet.Core.Constants;

namespace Theexonet.Core.Configuration;

public class TradeOptions
{
    public const string SectionName = "Trade";

    /// <summary>
    /// CSV spreadsheet for Trade Market items (buy supplies, sell ore). Opens in Excel.
    /// </summary>
    public string ItemsFile { get; set; } = "trade-items.csv";

    /// <summary>Percent of the final auction sale added to the public Trade Market value.</summary>
    public decimal AuctionFeePercent { get; set; } = TradeAuctionFormats.DefaultFeePercent;

    public int MinAuctionDurationMinutes { get; set; } = TradeAuctionFormats.MinDurationMinutes;

    public int MaxAuctionDurationMinutes { get; set; } = TradeAuctionFormats.MaxDurationMinutes;
}
