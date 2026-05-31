using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rava.Api.Services;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Core.Services;
using Rava.Infrastructure.Data;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api")]
public class StatusController(
    AppDbContext db,
    ServerRuntimeInfo runtime,
    IMarketDataProvider marketProvider,
    IMarketItemsCatalog marketItems,
    IOptionsMonitor<GameCreditsOptions> creditsOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<ActionResult<ApiStatusResponse>> Get(CancellationToken ct)
    {
        var databaseConnected = false;
        int? playerCount = null;

        try
        {
            databaseConnected = await db.Database.CanConnectAsync(ct);
            if (databaseConnected)
            {
                playerCount = await db.Players.CountAsync(ct);
            }
        }
        catch
        {
            // Report offline database status when the server is unreachable.
        }

        var databaseStatus = databaseConnected ? "online" : "offline";
        var status = databaseConnected ? "online" : "degraded";

        return Ok(new ApiStatusResponse(
            status,
            "Rava.Api",
            DateTime.UtcNow,
            databaseConnected,
            databaseStatus,
            playerCount,
            runtime.UptimeSeconds,
            runtime.StartedUtc,
            runtime.FirstRunUtc,
            GameVersion.Display));
    }

    [AllowAnonymous]
    [HttpGet("status/economy")]
    public async Task<ActionResult<PublicEconomyResponse>> GetEconomy(CancellationToken ct)
    {
        var today = UtcGameClock.Today;
        var credits = creditsOptions.CurrentValue;
        var referenceGameDay = 1;

        try
        {
            var world = await db.GameWorld.AsNoTracking().FirstOrDefaultAsync(ct);
            if (world is not null)
            {
                referenceGameDay = Math.Max(1, world.CurrentDay);
            }
        }
        catch
        {
            // Economy base prices still work when the database is offline.
        }

        var snapshot = await ResolveMarketSnapshotAsync(referenceGameDay, today, ct);

        var orePrices = Enum.GetValues<OreType>()
            .Select(oreType =>
            {
                var basePrice = marketItems.GetOreBasePrice(oreType);
                var buybackPrice = Math.Round(basePrice * GameBalance.EmergencyBuybackRate, 2);
                return new EconomyItemPriceDto(
                    oreType.ToString(),
                    "Ore",
                    basePrice,
                    basePrice,
                    null,
                    $"Emergency buyback: {buybackPrice} credits (50%)");
            })
            .ToList();

        var supplyPrices = snapshot.Prices
            .Select(price =>
            {
                var basePrice = marketItems.GetSupplyBasePrice(price.SupplyType);
                return new EconomyItemPriceDto(
                    price.SupplyType.ToString(),
                    "Supply",
                    price.Price,
                    basePrice,
                    price.ChangePct);
            })
            .ToList();

        return Ok(new PublicEconomyResponse(
            DateTime.UtcNow,
            referenceGameDay,
            snapshot.Source,
            today.ToString("yyyy-MM-dd"),
            GameBalance.EmergencyBuybackRate,
            credits.SignUp,
            credits.BirthdayBonus,
            orePrices,
            supplyPrices));
    }

    private async Task<DailyMarketSnapshot> ResolveMarketSnapshotAsync(
        int referenceGameDay,
        DateOnly utcDate,
        CancellationToken ct)
    {
        try
        {
            var cached = await db.MarketPriceHistory.AsNoTracking()
                .Where(m => m.UtcDate == utcDate)
                .ToListAsync(ct);

            if (cached.Count >= Enum.GetValues<SupplyType>().Length)
            {
                return new DailyMarketSnapshot
                {
                    GameDay = cached[0].GameDay,
                    Date = utcDate,
                    Source = cached[0].Source,
                    Prices = cached.Select(entry => new MarketPriceEntry
                    {
                        SupplyType = entry.SupplyType,
                        Price = entry.Price,
                        ChangePct = entry.ChangePct
                    }).ToList()
                };
            }
        }
        catch
        {
            // Fall back to the live/mock provider below.
        }

        return await marketProvider.GetDailyPricesAsync(referenceGameDay, utcDate, ct);
    }
}
