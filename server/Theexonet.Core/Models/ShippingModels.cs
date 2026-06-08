namespace Theexonet.Core.Models;

public class MineStockpileState
{
    public Guid Id { get; set; }
    public Guid MineId { get; set; }
    public string OreType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Condition { get; set; } = 100m;
}

public class ShippingRouteQuote
{
    public string RouteTier { get; set; } = string.Empty;
    public string ShipClass { get; set; } = string.Empty;
    public int TransitDays { get; set; }
    public int DepartureDay { get; set; }
    public int ArrivalDay { get; set; }
    public decimal FastLegPercent { get; set; }
    public decimal SlowLegPercent { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal Capacity { get; set; }
    public string RouteDescription { get; set; } = string.Empty;
}
