using Rava.Core.Enums;

namespace Rava.Api.Services;

public class MarketOptions
{
    public const string SectionName = "Market";

    public bool UseLiveData { get; set; } = true;

    public Dictionary<string, string> SupplySymbols { get; set; } = new()
    {
        [nameof(SupplyType.DrillBits)] = "CAT",
        [nameof(SupplyType.FuelCells)] = "XOM",
        [nameof(SupplyType.LifeSupport)] = "JNJ",
        [nameof(SupplyType.CommModules)] = "QCOM"
    };

    /// <summary>Reference US close prices used to scale live quotes into in-game supply prices.</summary>
    public Dictionary<string, decimal> ReferenceCloses { get; set; } = new()
    {
        ["CAT"] = 350m,
        ["XOM"] = 110m,
        ["JNJ"] = 155m,
        ["QCOM"] = 170m
    };
}
