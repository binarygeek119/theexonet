using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class TradeListingService(
    AppDbContext db,
    ITradeItemsCatalog tradeItems,
    ILiveUpdateBroadcaster liveUpdateBroadcaster)
{
    public async Task<TradeListingListResponse> GetListingsAsync(Guid? viewerPlayerId, CancellationToken ct)
    {
        await EnsureNpcStockpileAsync(ct);

        var listings = await db.MarketListings.AsNoTracking()
            .Include(l => l.Seller)
            .Where(l => l.Status == MarketListingStatuses.Active)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return new TradeListingListResponse(listings.Select(l => MapListing(l, viewerPlayerId)).ToList());
    }

    public async Task<(TradeListingActionResponse? Result, string? Error)> CreateListingAsync(
        Guid sellerId,
        CreateTradeListingRequest request,
        CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return (null, "Quantity must be positive.");
        }

        if (request.UnitPrice <= 0)
        {
            return (null, "Unit price must be positive.");
        }

        if (!Enum.TryParse<ItemCategory>(request.Category, true, out var category)
            || category is not (ItemCategory.Ore or ItemCategory.Supply))
        {
            return (null, "Category must be Ore or Supply.");
        }

        var itemType = request.ItemType?.Trim() ?? string.Empty;
        if (!IsTradeable(category, itemType))
        {
            return (null, "That item cannot be listed on the Trade Market.");
        }

        var preferNew = request.IsNew ?? false;
        var stack = await InventoryStackHelper.FindStackAsync(db, sellerId, category, itemType, preferNew, ct)
            ?? await InventoryStackHelper.FindStackAsync(db, sellerId, category, itemType, !preferNew, ct);

        if (stack is null || stack.Condition <= 0 || stack.Quantity < request.Quantity)
        {
            return (null, "Insufficient usable inventory for this listing.");
        }

        var condition = stack.Condition;
        if (!InventoryStackHelper.TryRemoveQuantity(stack, request.Quantity, out var removeError))
        {
            return (null, removeError);
        }

        if (stack.Quantity <= 0 && stack.BrokenQuantity <= 0)
        {
            db.Inventory.Remove(stack);
        }

        var listing = new MarketListingEntity
        {
            Id = Guid.NewGuid(),
            SellerPlayerId = sellerId,
            SellerType = MarketListingSellerTypes.Player,
            Category = category,
            ItemType = itemType,
            Quantity = request.Quantity,
            UnitPrice = Math.Round(request.UnitPrice, 2),
            Condition = condition,
            Status = MarketListingStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        };

        db.MarketListings.Add(listing);
        await db.SaveChangesAsync(ct);
        LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Auctions);

        return (new TradeListingActionResponse(true, "Listing posted on the Trade Market."), null);
    }

    public async Task<(TradeListingActionResponse? Result, string? Error)> PurchaseListingAsync(
        Guid buyerId,
        Guid listingId,
        CancellationToken ct)
    {
        var listing = await db.MarketListings
            .Include(l => l.Seller)
            .FirstOrDefaultAsync(l => l.Id == listingId && l.Status == MarketListingStatuses.Active, ct);

        if (listing is null)
        {
            return (null, "Listing not found.");
        }

        if (listing.SellerPlayerId == buyerId)
        {
            return (null, "You cannot buy your own listing.");
        }

        var buyer = await db.Players.FirstOrDefaultAsync(p => p.Id == buyerId, ct);
        if (buyer is null)
        {
            return (null, "Player not found.");
        }

        var totalCost = Math.Round(listing.UnitPrice * listing.Quantity, 2);
        if (buyer.Credits < totalCost)
        {
            return (null, "Insufficient Rax.");
        }

        buyer.Credits -= totalCost;

        if (listing.SellerType == MarketListingSellerTypes.Player && listing.SellerPlayerId is Guid sellerId)
        {
            var seller = await db.Players.FirstOrDefaultAsync(p => p.Id == sellerId, ct);
            if (seller is not null)
            {
                var fee = Math.Round(totalCost * GameBalance.TradeListingFeePercent / 100m, 2);
                var proceeds = totalCost - fee;
                seller.Credits += proceeds;

                var world = await GetOrCreateGameWorldAsync(ct);
                world.TradeMarketValue += fee;
            }
        }

        InventoryStackHelper.AddOrMerge(
            db,
            buyerId,
            listing.Category,
            listing.ItemType,
            listing.Quantity,
            listing.Condition,
            isNew: false);

        listing.Status = MarketListingStatuses.Sold;
        listing.SoldAt = DateTime.UtcNow;
        listing.BuyerPlayerId = buyerId;

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = buyerId,
            Type = TransactionType.SupplyPurchase,
            Amount = -totalCost,
            Description = $"Trade listing: {listing.Quantity:0.##} {listing.ItemType} ({listing.Condition:0}% condition)",
            GameDay = buyer.CurrentGameDay,
        });

        await db.SaveChangesAsync(ct);
        LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Auctions);
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, buyerId, LiveUpdateScopes.Mine);

        return (new TradeListingActionResponse(true, "Purchase complete.", buyer.Credits), null);
    }

    public async Task<(TradeListingActionResponse? Result, string? Error)> CancelListingAsync(
        Guid sellerId,
        Guid listingId,
        CancellationToken ct)
    {
        var listing = await db.MarketListings.FirstOrDefaultAsync(
            l => l.Id == listingId
                && l.SellerPlayerId == sellerId
                && l.Status == MarketListingStatuses.Active,
            ct);

        if (listing is null)
        {
            return (null, "Listing not found.");
        }

        InventoryStackHelper.AddOrMerge(
            db,
            sellerId,
            listing.Category,
            listing.ItemType,
            listing.Quantity,
            listing.Condition,
            isNew: false);

        listing.Status = MarketListingStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
        LiveUpdatePublisher.NotifyGlobalRefresh(liveUpdateBroadcaster, LiveUpdateScopes.Auctions);

        return (new TradeListingActionResponse(true, "Listing cancelled."), null);
    }

    public async Task EnsureNpcStockpileAsync(CancellationToken ct)
    {
        var tradeable = tradeItems.GetAllItems();
        foreach (var item in tradeable)
        {
            var activeCount = await db.MarketListings.CountAsync(l =>
                l.Status == MarketListingStatuses.Active
                && l.Category == item.Category
                && l.ItemType == item.ItemType, ct);

            var needed = GameBalance.MinTradeListingsPerItemType - activeCount;
            for (var i = 0; i < needed; i++)
            {
                SeedNpcListing(item.Category, item.ItemType, item.BasePrice);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private void SeedNpcListing(ItemCategory category, string itemType, decimal basePrice)
    {
        var rng = new Random(HashCode.Combine(category, itemType, DateTime.UtcNow.DayOfYear, Guid.NewGuid()));
        var condition = rng.Next(GameBalance.NpcConditionMin, GameBalance.NpcConditionMax + 1);
        var factor = ItemConditionCalculator.ConditionPriceFactor(condition);
        var unitPrice = Math.Round(
            basePrice * factor * GameBalance.NpcListingDiscount,
            2);
        var quantity = category == ItemCategory.Supply
            ? rng.Next(2, 8)
            : Math.Round((decimal)(rng.NextDouble() * 4 + 1), 1);

        db.MarketListings.Add(new MarketListingEntity
        {
            Id = Guid.NewGuid(),
            SellerPlayerId = null,
            SellerType = MarketListingSellerTypes.Npc,
            Category = category,
            ItemType = itemType,
            Quantity = quantity,
            UnitPrice = Math.Max(1m, unitPrice),
            Condition = condition,
            Status = MarketListingStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private TradeListingDto MapListing(MarketListingEntity listing, Guid? viewerPlayerId)
    {
        var display = GetDisplayName(listing.Category, listing.ItemType);
        var tradeItem = TryGetTradeItem(listing.Category, listing.ItemType);
        return new TradeListingDto(
            listing.Id,
            listing.SellerType,
            listing.SellerType == MarketListingSellerTypes.Npc
                ? "Belt Surplus Co."
                : listing.Seller?.Username,
            listing.Category.ToString(),
            listing.ItemType,
            display,
            listing.Quantity,
            listing.UnitPrice,
            listing.Condition,
            null,
            tradeItem?.Color,
            viewerPlayerId.HasValue && listing.SellerPlayerId == viewerPlayerId);
    }

    private TradeItemDefinition? TryGetTradeItem(ItemCategory category, string itemType)
    {
        try
        {
            if (category == ItemCategory.Ore && Enum.TryParse<OreType>(itemType, out var ore))
            {
                return tradeItems.GetOreItem(ore);
            }

            if (category == ItemCategory.Supply && Enum.TryParse<SupplyType>(itemType, out var supply))
            {
                return tradeItems.GetSupplyItem(supply);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private string GetDisplayName(ItemCategory category, string itemType)
    {
        var item = TryGetTradeItem(category, itemType);
        return item?.DisplayName ?? itemType;
    }

    private bool IsTradeable(ItemCategory category, string itemType)
    {
        if (category == ItemCategory.Ore && Enum.TryParse<OreType>(itemType, out var oreType))
        {
            return tradeItems.IsTradeableOre(oreType);
        }

        if (category == ItemCategory.Supply && Enum.TryParse<SupplyType>(itemType, out var supplyType))
        {
            return tradeItems.IsTradeableSupply(supplyType);
        }

        return false;
    }

    private async Task<GameWorldEntity> GetOrCreateGameWorldAsync(CancellationToken ct)
    {
        var world = await db.GameWorld.FirstOrDefaultAsync(w => w.Id == 1, ct);
        if (world is not null)
        {
            return world;
        }

        world = new GameWorldEntity { Id = 1 };
        db.GameWorld.Add(world);
        await db.SaveChangesAsync(ct);
        return world;
    }
}
