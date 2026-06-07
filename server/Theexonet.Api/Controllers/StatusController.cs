using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Theexonet.Api.Services;
using Theexonet.Api.Services.OpenAi;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api")]
public class StatusController(
    AppDbContext db,
    ServerRuntimeInfo runtime,
    ClientBuildInfo clientBuildInfo,
    IMarketDataProvider marketProvider,
    IMarketItemsCatalog marketItems,
    IGameCreditsConfig gameCreditsConfig,
    TradeAuctionService tradeAuctionService,
    OpenAiStatusDetailService openAiStatusDetailService,
    AiImageQueueService aiImageQueueService,
    IOptions<AiImageQueueOptions> aiImageQueueOptions) : ControllerBase
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
            "Theexonet.Api",
            DateTime.UtcNow,
            databaseConnected,
            databaseStatus,
            playerCount,
            runtime.UptimeSeconds,
            runtime.StartedUtc,
            runtime.FirstRunUtc,
            GameVersion.Display,
            clientBuildInfo.HtmlBuild));
    }

    [AllowAnonymous]
    [HttpGet("status/openai")]
    public async Task<ActionResult<PublicOpenAiStatusDetailResponse>> GetOpenAiStatus(CancellationToken ct) =>
        Ok(await openAiStatusDetailService.BuildAsync(ct));

    [AllowAnonymous]
    [HttpGet("status/ai-queue")]
    public async Task<ActionResult<PublicAiGenerationQueueStatusDto>> GetAiQueueStatus(CancellationToken ct)
    {
        if (!aiImageQueueOptions.Value.Enabled)
        {
            return Ok(new PublicAiGenerationQueueStatusDto(
                DateTime.UtcNow,
                false,
                "disabled",
                "AI generation queue is disabled in configuration.",
                null,
                0,
                0,
                0,
                new Dictionary<string, int>()));
        }

        var status = await aiImageQueueService.GetStatusAsync(kindFilter: null, ct);
        return Ok(new PublicAiGenerationQueueStatusDto(
            DateTime.UtcNow,
            true,
            status.Status,
            status.CurrentJobDescription,
            status.CurrentJobKind,
            status.QueuedCount,
            status.CompletedToday,
            status.FailedToday,
            status.QueuedByKind));
    }

    [AllowAnonymous]
    [HttpGet("status/economy")]
    public async Task<ActionResult<PublicEconomyResponse>> GetEconomy(CancellationToken ct)
    {
        var today = UtcGameClock.Today;
        var credits = gameCreditsConfig;
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
                    $"Emergency buy back: {buybackPrice} credits (50%)");
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

        var marketInfo = await tradeAuctionService.GetMarketInfoAsync(ct);

        return Ok(new PublicEconomyResponse(
            DateTime.UtcNow,
            referenceGameDay,
            snapshot.Source,
            today.ToString("yyyy-MM-dd"),
            GameBalance.EmergencyBuybackRate,
            credits.SignUp,
            credits.BirthdayBonus,
            marketInfo.TradeMarketValue,
            marketInfo.AuctionFeePercent,
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
