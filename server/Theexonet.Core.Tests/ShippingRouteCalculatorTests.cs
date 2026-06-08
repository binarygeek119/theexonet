using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class ShippingRouteCalculatorTests
{
    [Fact]
    public void BuildQuote_Express_HasPartialFastLeg()
    {
        var quote = ShippingRouteCalculator.BuildQuote(ShipClass.Freighter, ShippingRouteTier.Express, 10);
        Assert.Equal(0.45m, quote.FastLegPercent);
        Assert.True(quote.SlowLegPercent > 0);
        Assert.Equal(8, quote.DepartureDay);
    }

    [Fact]
    public void ComputeShippingCost_PenalizesUnderfill()
    {
        var full = ShippingRouteCalculator.ComputeShippingCost(
            ShipClass.Hauler, ShippingRouteTier.Standard, 50m, 1m);
        var partial = ShippingRouteCalculator.ComputeShippingCost(
            ShipClass.Hauler, ShippingRouteTier.Standard, 50m, 0.5m);
        Assert.True(partial > full);
    }

    [Fact]
    public void BulkShip_HasHigherCapacityThanScout()
    {
        Assert.True(
            ShippingRouteCalculator.GetShipCapacity(ShipClass.Bulk)
            > ShippingRouteCalculator.GetShipCapacity(ShipClass.Scout));
    }
}
