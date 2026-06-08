using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public static class ShippingRouteCalculator
{
    public static decimal GetShipCapacity(ShipClass shipClass) =>
        GameBalance.ShipCapacity[shipClass];

    public static int GetTransitDays(ShippingRouteTier route) =>
        GameBalance.RouteTransitDays[route];

    public static decimal ComputeShippingCost(
        ShipClass shipClass,
        ShippingRouteTier route,
        decimal cargoQuantity,
        decimal fillPercent)
    {
        var baseCost = GameBalance.RouteCostPerUnit[route]
            * cargoQuantity
            * GameBalance.ShipBaseCostMultiplier[shipClass];

        if (route == ShippingRouteTier.Express && cargoQuantity < GameBalance.ExpressMinCargoForEfficiency)
        {
            baseCost *= 1.2m;
        }

        if (fillPercent < GameBalance.MinEfficientShipmentFill)
        {
            baseCost *= GameBalance.UnderfillCostPenaltyMultiplier;
        }

        return Math.Round(baseCost, 2);
    }

    public static ShippingRouteQuote BuildQuote(
        ShipClass shipClass,
        ShippingRouteTier route,
        int arrivalDay)
    {
        var transitDays = GetTransitDays(route);
        var fastLeg = GameBalance.RouteFastLegPercent[route];
        var capacity = GetShipCapacity(shipClass);
        var departureDay = Math.Max(1, arrivalDay - transitDays);

        return new ShippingRouteQuote
        {
            RouteTier = route.ToString(),
            ShipClass = shipClass.ToString(),
            TransitDays = transitDays,
            DepartureDay = departureDay,
            ArrivalDay = arrivalDay,
            FastLegPercent = fastLeg,
            SlowLegPercent = 1m - fastLeg,
            Capacity = capacity,
            EstimatedCost = ComputeShippingCost(shipClass, route, capacity, 1m),
            RouteDescription = DescribeRoute(route, fastLeg, transitDays),
        };
    }

    private static string DescribeRoute(ShippingRouteTier route, decimal fastLeg, int days)
    {
        if (fastLeg <= 0)
        {
            return $"Economy belt haul — {days} days on standard lanes only.";
        }

        var fastPct = Math.Round(fastLeg * 100m, 0);
        var slowPct = 100m - fastPct;
        return $"{route} route — {fastPct}% high-speed relay (cannot cover full run), {slowPct}% standard/slow legs · {days} days total.";
    }
}
