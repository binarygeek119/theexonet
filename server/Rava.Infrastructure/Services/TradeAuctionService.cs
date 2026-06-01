using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class TradeAuctionService(
    AppDbContext db,
    ITradeItemsCatalog tradeItems,
    IOptions<TradeOptions> tradeOptions)
{
    private TradeOptions Options => tradeOptions.Value;

    public async Task<TradeMarketInfoResponse> GetMarketInfoAsync(CancellationToken ct)
    {
        var world = await GetOrCreateGameWorldAsync(ct);
        return new TradeMarketInfoResponse(
            world.TradeMarketValue,
            Options.AuctionFeePercent,
            Options.MinAuctionDurationMinutes,
            Options.MaxAuctionDurationMinutes);
    }

    public async Task<TradeAuctionListResponse> GetActiveAuctionsAsync(Guid? viewerPlayerId, CancellationToken ct)
    {
        await ProcessDueAuctionsAsync(ct);
        var world = await GetOrCreateGameWorldAsync(ct);

        var auctions = await db.TradeAuctions.AsNoTracking()
            .Include(a => a.Seller)
            .Include(a => a.HighBidder)
            .Where(a => a.Status == TradeAuctionStatuses.Open || a.Status == TradeAuctionStatuses.Active)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return new TradeAuctionListResponse(
            world.TradeMarketValue,
            Options.AuctionFeePercent,
            auctions.Select(a => MapAuction(a, viewerPlayerId)).ToList());
    }

    public async Task<(TradeAuctionActionResponse? Result, string? Error)> CreateAuctionAsync(
        Guid sellerId,
        CreateTradeAuctionRequest request,
        CancellationToken ct)
    {
        await ProcessDueAuctionsAsync(ct);

        if (request.Quantity < TradeAuctionFormats.MinQuantity)
        {
            return (null, "Quantity must be positive.");
        }

        if (request.StartPrice < TradeAuctionFormats.MinStartPrice)
        {
            return (null, $"Start price must be at least {TradeAuctionFormats.MinStartPrice:0} Rax.");
        }

        if (request.DurationMinutes < Options.MinAuctionDurationMinutes
            || request.DurationMinutes > Options.MaxAuctionDurationMinutes)
        {
            return (null,
                $"Auction duration must be between {Options.MinAuctionDurationMinutes} and {Options.MaxAuctionDurationMinutes} minutes.");
        }

        if (!Enum.TryParse<ItemCategory>(request.Category, true, out var category)
            || category is not (ItemCategory.Ore or ItemCategory.Supply))
        {
            return (null, "Category must be Ore or Supply.");
        }

        var itemType = request.ItemType?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(itemType))
        {
            return (null, "Item type is required.");
        }

        if (!IsTradeable(category, itemType))
        {
            return (null, "That item cannot be auctioned on the Trade Market.");
        }

        var seller = await db.Players.FirstOrDefaultAsync(p => p.Id == sellerId, ct);
        if (seller is null)
        {
            return (null, "Player not found.");
        }

        var inventory = await db.Inventory.FirstOrDefaultAsync(i =>
            i.PlayerId == sellerId &&
            i.Category == category &&
            i.ItemType == itemType, ct);

        if (inventory is null || inventory.Quantity < request.Quantity)
        {
            return (null, "Insufficient inventory for this auction.");
        }

        inventory.Quantity -= request.Quantity;
        if (inventory.Quantity <= 0)
        {
            db.Inventory.Remove(inventory);
        }

        var auction = new TradeAuctionEntity
        {
            Id = Guid.NewGuid(),
            SellerPlayerId = sellerId,
            Category = category,
            ItemType = itemType,
            Quantity = request.Quantity,
            StartPrice = Math.Round(request.StartPrice, 2),
            DurationMinutes = request.DurationMinutes,
            Status = TradeAuctionStatuses.Open,
            CreatedAt = DateTime.UtcNow
        };

        db.TradeAuctions.Add(auction);
        await db.SaveChangesAsync(ct);

        return (new TradeAuctionActionResponse(true, "Auction listed on the Trade Market."), null);
    }

    public async Task<(TradeAuctionActionResponse? Result, string? Error)> PlaceBidAsync(
        Guid bidderId,
        Guid auctionId,
        decimal bidAmount,
        CancellationToken ct)
    {
        await ProcessDueAuctionsAsync(ct);

        var auction = await db.TradeAuctions
            .Include(a => a.Seller)
            .FirstOrDefaultAsync(a => a.Id == auctionId, ct);

        if (auction is null
            || auction.Status is not (TradeAuctionStatuses.Open or TradeAuctionStatuses.Active))
        {
            return (null, "Auction not found.");
        }

        if (auction.SellerPlayerId == bidderId)
        {
            return (null, "You cannot bid on your own auction.");
        }

        if (auction.Status == TradeAuctionStatuses.Active
            && auction.EndsAt is not null
            && auction.EndsAt <= DateTime.UtcNow)
        {
            return (null, "This auction has ended.");
        }

        bidAmount = Math.Round(bidAmount, 2);
        var minimumBid = GetMinimumNextBid(auction);
        if (bidAmount < minimumBid)
        {
            return (null, $"Bid must be at least {minimumBid:0.##} Rax.");
        }

        var bidder = await db.Players.FirstOrDefaultAsync(p => p.Id == bidderId, ct);
        if (bidder is null)
        {
            return (null, "Player not found.");
        }

        if (bidder.Credits < bidAmount)
        {
            return (null, "Insufficient Rax for this bid.");
        }

        if (auction.HighBidderPlayerId is not null && auction.CurrentBid is not null)
        {
            var previousBidder = await db.Players.FirstOrDefaultAsync(
                p => p.Id == auction.HighBidderPlayerId.Value,
                ct);
            if (previousBidder is not null)
            {
                previousBidder.Credits += auction.CurrentBid.Value;
            }
        }

        bidder.Credits -= bidAmount;
        auction.CurrentBid = bidAmount;
        auction.HighBidderPlayerId = bidderId;

        if (auction.Status == TradeAuctionStatuses.Open)
        {
            auction.Status = TradeAuctionStatuses.Active;
            auction.EndsAt = DateTime.UtcNow.AddMinutes(auction.DurationMinutes);
        }

        await db.SaveChangesAsync(ct);

        var message = auction.EndsAt is null
            ? "Bid placed."
            : $"Bid placed. Auction ends {auction.EndsAt.Value.ToUniversalTime():u}.";

        return (new TradeAuctionActionResponse(true, message, bidder.Credits), null);
    }

    public async Task<(TradeAuctionActionResponse? Result, string? Error)> CancelAuctionAsync(
        Guid sellerId,
        Guid auctionId,
        CancellationToken ct)
    {
        await ProcessDueAuctionsAsync(ct);

        var auction = await db.TradeAuctions.FirstOrDefaultAsync(
            a => a.Id == auctionId && a.SellerPlayerId == sellerId,
            ct);

        if (auction is null || auction.Status != TradeAuctionStatuses.Open)
        {
            return (null, "Auction not found or cannot be cancelled after the first bid.");
        }

        if (auction.CurrentBid is not null)
        {
            return (null, "Auctions with bids cannot be cancelled.");
        }

        await ReturnInventoryAsync(auction.SellerPlayerId, auction.Category, auction.ItemType, auction.Quantity, ct);
        auction.Status = TradeAuctionStatuses.Cancelled;
        auction.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return (new TradeAuctionActionResponse(true, "Auction cancelled."), null);
    }

    public async Task ProcessDueAuctionsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await db.TradeAuctions
            .Where(a => a.Status == TradeAuctionStatuses.Active
                && a.EndsAt != null
                && a.EndsAt <= now)
            .ToListAsync(ct);

        foreach (var auction in due)
        {
            if (auction.HighBidderPlayerId is null || auction.CurrentBid is null)
            {
                await ExpireWithoutSaleAsync(auction, ct);
                continue;
            }

            await CompleteSaleAsync(auction, ct);
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task CompleteSaleAsync(TradeAuctionEntity auction, CancellationToken ct)
    {
        var seller = await db.Players.FirstOrDefaultAsync(p => p.Id == auction.SellerPlayerId, ct);
        var buyer = await db.Players.FirstOrDefaultAsync(p => p.Id == auction.HighBidderPlayerId!.Value, ct);
        if (seller is null || buyer is null || auction.CurrentBid is null)
        {
            await ExpireWithoutSaleAsync(auction, ct);
            return;
        }

        var finalBid = auction.CurrentBid.Value;
        var fee = Math.Round(finalBid * Options.AuctionFeePercent / 100m, 2);
        var sellerProceeds = finalBid - fee;
        if (sellerProceeds < 0)
        {
            sellerProceeds = 0;
            fee = finalBid;
        }

        seller.Credits += sellerProceeds;
        var world = await GetOrCreateGameWorldAsync(ct);
        world.TradeMarketValue += fee;

        await AddInventoryAsync(buyer.Id, auction.Category, auction.ItemType, auction.Quantity, ct);

        auction.Status = TradeAuctionStatuses.Sold;
        auction.CompletedAt = DateTime.UtcNow;

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = buyer.Id,
            Type = TransactionType.TradeAuctionPurchase,
            Amount = -finalBid,
            Description = $"Auction won: {auction.Quantity:0.##} {auction.ItemType}",
            GameDay = buyer.CurrentGameDay
        });

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = seller.Id,
            Type = TransactionType.TradeAuctionSale,
            Amount = sellerProceeds,
            Description =
                $"Auction sold: {auction.Quantity:0.##} {auction.ItemType} ({Options.AuctionFeePercent:0.##}% market fee)",
            GameDay = seller.CurrentGameDay
        });
    }

    private async Task ExpireWithoutSaleAsync(TradeAuctionEntity auction, CancellationToken ct)
    {
        if (auction.HighBidderPlayerId is not null && auction.CurrentBid is not null)
        {
            var bidder = await db.Players.FirstOrDefaultAsync(p => p.Id == auction.HighBidderPlayerId.Value, ct);
            if (bidder is not null)
            {
                bidder.Credits += auction.CurrentBid.Value;
            }
        }

        await ReturnInventoryAsync(auction.SellerPlayerId, auction.Category, auction.ItemType, auction.Quantity, ct);
        auction.Status = TradeAuctionStatuses.Expired;
        auction.CompletedAt = DateTime.UtcNow;
        auction.CurrentBid = null;
        auction.HighBidderPlayerId = null;
        auction.EndsAt = null;
    }

    private async Task ReturnInventoryAsync(
        Guid playerId,
        ItemCategory category,
        string itemType,
        decimal quantity,
        CancellationToken ct)
    {
        await AddInventoryAsync(playerId, category, itemType, quantity, ct);
    }

    private async Task AddInventoryAsync(
        Guid playerId,
        ItemCategory category,
        string itemType,
        decimal quantity,
        CancellationToken ct)
    {
        var item = await db.Inventory.FirstOrDefaultAsync(i =>
            i.PlayerId == playerId &&
            i.Category == category &&
            i.ItemType == itemType, ct);

        if (item is null)
        {
            db.Inventory.Add(new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Category = category,
                ItemType = itemType,
                Quantity = quantity
            });
        }
        else
        {
            item.Quantity += quantity;
        }
    }

    private async Task<GameWorldEntity> GetOrCreateGameWorldAsync(CancellationToken ct)
    {
        var world = await db.GameWorld.FirstOrDefaultAsync(w => w.Id == 1, ct);
        if (world is not null)
        {
            return world;
        }

        world = new GameWorldEntity
        {
            Id = 1,
            CurrentDay = 1,
            LastTickAt = DateTime.UtcNow,
            MarketSeed = 42,
            TradeMarketValue = 0
        };
        db.GameWorld.Add(world);
        await db.SaveChangesAsync(ct);
        return world;
    }

    private TradeAuctionDto MapAuction(TradeAuctionEntity auction, Guid? viewerPlayerId)
    {
        var secondsRemaining = auction.EndsAt is null
            ? null
            : (int?)Math.Max(0, (int)Math.Ceiling((auction.EndsAt.Value - DateTime.UtcNow).TotalSeconds));

        return new TradeAuctionDto(
            auction.Id,
            auction.Seller.Username,
            auction.Category.ToString(),
            auction.ItemType,
            GetDisplayName(auction.Category, auction.ItemType),
            auction.Quantity,
            auction.StartPrice,
            auction.CurrentBid,
            auction.HighBidder?.Username,
            auction.DurationMinutes,
            auction.EndsAt,
            auction.Status,
            viewerPlayerId.HasValue && auction.SellerPlayerId == viewerPlayerId.Value,
            GetMinimumNextBid(auction),
            secondsRemaining);
    }

    private decimal GetMinimumNextBid(TradeAuctionEntity auction)
    {
        if (auction.CurrentBid is null)
        {
            return auction.StartPrice;
        }

        var increment = Math.Max(1m, Math.Round(auction.CurrentBid.Value * 0.05m, 2));
        return Math.Round(auction.CurrentBid.Value + increment, 2);
    }

    private string GetDisplayName(ItemCategory category, string itemType)
    {
        try
        {
            if (category == ItemCategory.Ore && Enum.TryParse<OreType>(itemType, out var oreType))
            {
                return tradeItems.GetOreItem(oreType).DisplayName;
            }

            if (category == ItemCategory.Supply && Enum.TryParse<SupplyType>(itemType, out var supplyType))
            {
                return tradeItems.GetSupplyItem(supplyType).DisplayName;
            }
        }
        catch
        {
            // Fall back to raw item type.
        }

        return itemType;
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
}
