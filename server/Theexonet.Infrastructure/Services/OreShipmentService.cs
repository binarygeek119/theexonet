using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class OreShipmentService(
    AppDbContext db,
    IMarketItemsCatalog marketItems)
{
    public async Task<ShippingDashboardResponse> GetDashboardAsync(
        Guid playerId,
        Guid mineId,
        int currentGameDay,
        CancellationToken ct)
    {
        var stockpile = await db.MineOreStockpile.AsNoTracking()
            .Where(s => s.MineId == mineId)
            .ToListAsync(ct);

        var shipments = await db.OreShipments.AsNoTracking()
            .Where(s => s.MineId == mineId && s.PlayerId == playerId)
            .OrderBy(s => s.ScheduledArrivalDay)
            .ToListAsync(ct);

        var totalStockpile = stockpile.Sum(s => s.Quantity);
        var routes = Enum.GetValues<ShippingRouteTier>()
            .SelectMany(route => Enum.GetValues<ShipClass>().Select(ship =>
                ShippingRouteCalculator.BuildQuote(ship, route, currentGameDay + GetTransitDays(route))))
            .ToList();

        return new ShippingDashboardResponse(
            GameBalance.OreStockpileCapacity,
            totalStockpile,
            totalStockpile >= GameBalance.OreStockpileCapacity,
            stockpile.Select(s => new StockpileItemDto(s.OreType, s.Quantity, s.Condition)).ToList(),
            shipments.Select(MapShipment).ToList(),
            routes.Select(r => new ShippingRouteOptionDto(
                r.ShipClass,
                r.RouteTier,
                r.TransitDays,
                r.Capacity,
                r.FastLegPercent,
                r.SlowLegPercent,
                r.EstimatedCost,
                r.RouteDescription)).ToList());
    }

    public async Task<(bool Success, string Message, Guid? ShipmentId)> ScheduleShipmentAsync(
        Guid playerId,
        Guid mineId,
        ScheduleShipmentRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ShipClass>(request.ShipClass, true, out var shipClass))
        {
            return (false, "Invalid ship class.", null);
        }

        if (!Enum.TryParse<ShippingRouteTier>(request.RouteTier, true, out var route))
        {
            return (false, "Invalid route.", null);
        }

        if (!Enum.TryParse<OreType>(request.OreType, true, out var oreType))
        {
            return (false, "Invalid ore type.", null);
        }

        if (request.ScheduledArrivalDay <= 0)
        {
            return (false, "Arrival day must be in the future.", null);
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (false, "Player not found.", null);
        }

        if (request.ScheduledArrivalDay <= player.CurrentGameDay)
        {
            return (false, "Arrival day must be after the current game day.", null);
        }

        var transitDays = ShippingRouteCalculator.GetTransitDays(route);
        var departureDay = request.ScheduledArrivalDay - transitDays;
        if (departureDay <= player.CurrentGameDay)
        {
            return (false, $"Route too slow for day {request.ScheduledArrivalDay}. Pick a later arrival or faster route.", null);
        }

        var capacity = ShippingRouteCalculator.GetShipCapacity(shipClass);
        var quote = ShippingRouteCalculator.BuildQuote(shipClass, route, request.ScheduledArrivalDay);

        var shipment = new OreShipmentEntity
        {
            Id = Guid.NewGuid(),
            MineId = mineId,
            PlayerId = playerId,
            ShipClass = shipClass,
            RouteTier = route,
            OreType = oreType.ToString(),
            Capacity = capacity,
            ScheduledArrivalDay = request.ScheduledArrivalDay,
            DepartureDay = departureDay,
            DaysRemaining = transitDays,
            Status = ShipmentStatusNames.Scheduled,
            FastLegPercent = quote.FastLegPercent,
            CreatedAt = DateTime.UtcNow,
        };

        db.OreShipments.Add(shipment);
        await db.SaveChangesAsync(ct);

        return (true, $"Shipment scheduled — departs day {departureDay}, arrives day {request.ScheduledArrivalDay}.", shipment.Id);
    }

    public async Task<(bool Success, string Message)> CancelShipmentAsync(
        Guid playerId,
        Guid mineId,
        Guid shipmentId,
        CancellationToken ct)
    {
        var shipment = await db.OreShipments.FirstOrDefaultAsync(
            s => s.Id == shipmentId && s.MineId == mineId && s.PlayerId == playerId, ct);

        if (shipment is null || shipment.Status != ShipmentStatusNames.Scheduled)
        {
            return (false, "Scheduled shipment not found.");
        }

        shipment.Status = ShipmentStatusNames.Cancelled;
        shipment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, "Shipment cancelled.");
    }

    public async Task<IReadOnlyList<string>> ProcessDayAsync(
        Guid playerId,
        Guid mineId,
        int gameDay,
        CancellationToken ct)
    {
        var messages = new List<string>();
        var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

        var departures = await db.OreShipments
            .Where(s => s.MineId == mineId
                && s.PlayerId == playerId
                && s.Status == ShipmentStatusNames.Scheduled
                && s.DepartureDay == gameDay)
            .ToListAsync(ct);

        foreach (var shipment in departures)
        {
            var msg = await DepartShipmentAsync(player, shipment, ct);
            if (!string.IsNullOrEmpty(msg))
            {
                messages.Add(msg);
            }
        }

        var inTransit = await db.OreShipments
            .Where(s => s.MineId == mineId
                && s.PlayerId == playerId
                && (s.Status == ShipmentStatusNames.InTransit || s.Status == ShipmentStatusNames.Delayed))
            .ToListAsync(ct);

        foreach (var shipment in inTransit)
        {
            var msg = await AdvanceTransitAsync(player, shipment, gameDay, ct);
            if (!string.IsNullOrEmpty(msg))
            {
                messages.Add(msg);
            }
        }

        return messages;
    }

    private async Task<string?> DepartShipmentAsync(
        PlayerEntity player,
        OreShipmentEntity shipment,
        CancellationToken ct)
    {
        var stock = await db.MineOreStockpile.FirstOrDefaultAsync(
            s => s.MineId == shipment.MineId && s.OreType == shipment.OreType, ct);

        var available = stock?.Quantity ?? 0m;
        var loadQty = Math.Min(shipment.Capacity, available);
        var fillPercent = shipment.Capacity <= 0 ? 0m : loadQty / shipment.Capacity;

        if (loadQty <= 0)
        {
            shipment.Status = ShipmentStatusNames.Failed;
            shipment.LastEventDescription = "Departed empty — no ore in stockpile.";
            shipment.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return $"Shipment to day {shipment.ScheduledArrivalDay} departed empty — no {shipment.OreType} in stockpile.";
        }

        var condition = stock!.Condition;
        stock.Quantity -= loadQty;
        if (stock.Quantity <= 0)
        {
            db.MineOreStockpile.Remove(stock);
        }

        var cost = ShippingRouteCalculator.ComputeShippingCost(
            shipment.ShipClass,
            shipment.RouteTier,
            loadQty,
            fillPercent);

        if (player.Credits < cost)
        {
            stock.Quantity += loadQty;
            shipment.Status = ShipmentStatusNames.Failed;
            shipment.LastEventDescription = "Insufficient Rax for freight fees.";
            shipment.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return $"Shipment cancelled — insufficient Rax for freight ({cost} required).";
        }

        player.Credits -= cost;
        shipment.CargoQuantity = loadQty;
        shipment.CargoCondition = condition;
        shipment.FillPercent = Math.Round(fillPercent * 100m, 1);
        shipment.ShippingCostPaid = cost;
        shipment.Status = ShipmentStatusNames.InTransit;
        shipment.DaysRemaining = ShippingRouteCalculator.GetTransitDays(shipment.RouteTier);

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Type = TransactionType.ShippingCost,
            Amount = -cost,
            Description = $"Freight {loadQty:0.##} {shipment.OreType} ({shipment.RouteTier}, {shipment.FillPercent:0}% fill)",
            GameDay = player.CurrentGameDay,
        });

        await db.SaveChangesAsync(ct);

        var fillNote = fillPercent < GameBalance.MinEfficientShipmentFill
            ? " Under-filled — freight penalty applied."
            : string.Empty;

        return $"Freighter departed with {loadQty:0.##} {shipment.OreType} ({shipment.FillPercent:0}% capacity).{fillNote}";
    }

    private async Task<string?> AdvanceTransitAsync(
        PlayerEntity player,
        OreShipmentEntity shipment,
        int gameDay,
        CancellationToken ct)
    {
        var rng = new Random(HashCode.Combine(shipment.Id, gameDay));

        if (rng.NextDouble() < (double)GameBalance.TransitCrashChancePerDay)
        {
            shipment.Status = ShipmentStatusNames.Failed;
            shipment.CargoQuantity = 0;
            shipment.LastEventDescription = "Convoy lost in transit crash.";
            shipment.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return $"Shipment lost — convoy crash on {shipment.OreType} run.";
        }

        if (rng.NextDouble() < (double)GameBalance.TransitRobberyChancePerDay)
        {
            var loss = Math.Round(shipment.CargoQuantity * GameBalance.RobberyCargoLossPercent, 2);
            shipment.CargoQuantity -= loss;
            shipment.LastEventDescription = $"Pirates seized {loss} units.";
            if (shipment.CargoQuantity <= 0)
            {
                shipment.Status = ShipmentStatusNames.Failed;
                shipment.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return "Shipment lost — cargo taken by raiders.";
            }
        }

        if (rng.NextDouble() < (double)GameBalance.TransitDelayChancePerDay)
        {
            shipment.DaysRemaining += 1;
            shipment.Status = ShipmentStatusNames.Delayed;
            shipment.LastEventDescription = PickDelayReason(rng);
            await db.SaveChangesAsync(ct);
            return $"Shipment delayed — {shipment.LastEventDescription}";
        }

        shipment.DaysRemaining -= 1;
        if (shipment.DaysRemaining > 0)
        {
            shipment.Status = ShipmentStatusNames.InTransit;
            await db.SaveChangesAsync(ct);
            return null;
        }

        return await CompleteArrivalAsync(player, shipment, ct);
    }

    private async Task<string?> CompleteArrivalAsync(
        PlayerEntity player,
        OreShipmentEntity shipment,
        CancellationToken ct)
    {
        if (!Enum.TryParse<OreType>(shipment.OreType, out var oreType))
        {
            shipment.Status = ShipmentStatusNames.Failed;
            shipment.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return "Shipment failed — invalid ore manifest.";
        }

        var basePrice = marketItems.GetOreBasePrice(oreType);
        var factor = ItemConditionCalculator.ConditionPriceFactor(shipment.CargoCondition);
        var saleValue = Math.Round(basePrice * shipment.CargoQuantity * factor, 2);

        player.Credits += saleValue;
        shipment.Status = ShipmentStatusNames.Arrived;
        shipment.CompletedAt = DateTime.UtcNow;
        shipment.LastEventDescription = $"Delivered to refinery for {saleValue} Rax.";

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Type = TransactionType.ShipmentSale,
            Amount = saleValue,
            Description = $"Shipment arrived: {shipment.CargoQuantity:0.##} {shipment.OreType}",
            GameDay = player.CurrentGameDay,
        });

        await db.SaveChangesAsync(ct);
        return $"Shipment arrived — sold {shipment.CargoQuantity:0.##} {shipment.OreType} for {saleValue} Rax.";
    }

    private static string PickDelayReason(Random rng)
    {
        string[] reasons =
        [
            "engine breakdown in slow lane",
            "customs inspector hold",
            "political corridor closure",
            "relay queue at high-speed handoff",
            "debris field detour",
        ];
        return reasons[rng.Next(reasons.Length)];
    }

    private static int GetTransitDays(ShippingRouteTier route) =>
        GameBalance.RouteTransitDays[route];

    private static ShipmentDto MapShipment(OreShipmentEntity s) => new(
        s.Id,
        s.ShipClass.ToString(),
        s.RouteTier.ToString(),
        s.OreType,
        s.Capacity,
        s.ScheduledArrivalDay,
        s.DepartureDay,
        s.DaysRemaining,
        s.Status,
        s.CargoQuantity,
        s.CargoCondition,
        s.FillPercent,
        s.ShippingCostPaid,
        s.FastLegPercent,
        s.LastEventDescription);
}
