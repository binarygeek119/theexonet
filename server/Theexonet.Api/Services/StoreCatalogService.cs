using Theexonet.Api.Services.VoidCorp;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Theexonet.Api.Services;

public sealed class StoreCatalogService(
    AppDbContext db,
    VoidCorpCatalogService voidCorpCatalog,
    ITradeItemsCatalog tradeItems,
    PlayerGameService playerGameService)
{
    public async Task<StoreCatalogResponse> GetCatalogAsync(Guid? playerId, CancellationToken ct)
    {
        var voidCorp = voidCorpCatalog.GetCatalog();
        var voidBySlug = voidCorp.Products.ToDictionary(p => p.Slug, StringComparer.OrdinalIgnoreCase);

        int gameDay = 1;
        string source = "catalog";
        IReadOnlyDictionary<SupplyType, decimal> livePrices = new Dictionary<SupplyType, decimal>();

        if (playerId is Guid pid)
        {
            var market = await playerGameService.GetMarketTodayAsync(pid, ct);
            if (market is not null)
            {
                gameDay = market.GameDay;
                source = market.Source;
                livePrices = market.Prices.ToDictionary(p => (SupplyType)p.SupplyType, p => p.Price);
            }
        }
        else
        {
            var world = await db.GameWorld.AsNoTracking().FirstOrDefaultAsync(w => w.Id == 1, ct);
            gameDay = world?.CurrentDay ?? 1;
        }

        var products = new List<StoreProductDto>();
        foreach (var supply in tradeItems.GetSupplyItems())
        {
            var slug = supply.ItemType;
            voidBySlug.TryGetValue(slug, out var vc);
            var livePrice = livePrices.TryGetValue(Enum.Parse<SupplyType>(supply.ItemType), out var price)
                ? price
                : supply.BasePrice;

            products.Add(new StoreProductDto(
                slug,
                supply.ItemType,
                supply.Category.ToString(),
                vc?.DisplayName ?? supply.DisplayName,
                vc?.Tagline ?? "Factory-new industrial supply",
                vc?.Summary ?? supply.DisplayName,
                vc?.Description ?? $"Brand-new {supply.DisplayName} — never used, VoidCorp certified.",
                supply.BasePrice,
                livePrice,
                vc?.Color ?? supply.Color,
                vc?.UiSymbol ?? supply.UiSymbol,
                vc?.ImageUrl));
        }

        return new StoreCatalogResponse(gameDay, source, products);
    }

    public StoreProductDto? GetProduct(string slug)
    {
        var catalog = GetCatalogAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        return catalog.Products.FirstOrDefault(p =>
            p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)
            || p.ItemType.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }
}
