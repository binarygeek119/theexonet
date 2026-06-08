using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Core.Tests;

public class TradeListingServiceTests
{
    private sealed class NoOpBroadcaster : ILiveUpdateBroadcaster
    {
        public IAsyncEnumerable<LiveUpdateEventDto> SubscribeAsync(Guid playerId, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<LiveUpdateEventDto>();

        public void PublishToPlayer(Guid playerId, LiveUpdateEventDto evt) { }

        public void PublishGlobal(LiveUpdateEventDto evt) { }
    }

    [Fact]
    public async Task GetListingsAsync_SeedsNpcStockpileWhenEmpty()
    {
        await using var db = CreateDb(nameof(GetListingsAsync_SeedsNpcStockpileWhenEmpty));
        var service = new TradeListingService(db, TradeItemsCatalog.CreateDefault(), new NoOpBroadcaster());

        var response = await service.GetListingsAsync(null, CancellationToken.None);

        Assert.NotEmpty(response.Listings);
        Assert.Contains(response.Listings, l => l.SellerType == MarketListingSellerTypes.Npc);
    }

    [Fact]
    public async Task CreateAndPurchaseListing_TransfersUsedInventory()
    {
        await using var db = CreateDb(nameof(CreateAndPurchaseListing_TransfersUsedInventory));
        var (sellerId, buyerId) = await SeedPlayersAsync(db, sellerCredits: 0m, buyerCredits: 500m);
        db.Inventory.Add(new InventoryItemEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = sellerId,
            Category = ItemCategory.Supply,
            ItemType = nameof(SupplyType.DrillBits),
            Quantity = 5m,
            Condition = 72m,
            IsNew = false,
        });
        await db.SaveChangesAsync();

        var service = new TradeListingService(db, TradeItemsCatalog.CreateDefault(), new NoOpBroadcaster());
        var (createResult, createErr) = await service.CreateListingAsync(
            sellerId,
            new CreateTradeListingRequest("Supply", nameof(SupplyType.DrillBits), 2m, 40m, false),
            CancellationToken.None);

        Assert.Null(createErr);
        Assert.True(createResult!.Success);

        var listing = await db.MarketListings.SingleAsync(l =>
            l.SellerPlayerId == sellerId && l.Status == MarketListingStatuses.Active);
        var (buyResult, buyErr) = await service.PurchaseListingAsync(buyerId, listing.Id, CancellationToken.None);

        Assert.Null(buyErr);
        Assert.True(buyResult!.Success);

        var buyerStack = await db.Inventory.SingleAsync(i => i.PlayerId == buyerId);
        Assert.Equal(2m, buyerStack.Quantity);
        Assert.Equal(72m, buyerStack.Condition);
        Assert.False(buyerStack.IsNew);
    }

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid SellerId, Guid BuyerId)> SeedPlayersAsync(
        AppDbContext db,
        decimal sellerCredits,
        decimal buyerCredits)
    {
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        db.Players.AddRange(
            new PlayerEntity
            {
                Id = sellerId,
                Username = "seller",
                Email = "seller@test",
                PasswordHash = "hash",
                Credits = sellerCredits,
            },
            new PlayerEntity
            {
                Id = buyerId,
                Username = "buyer",
                Email = "buyer@test",
                PasswordHash = "hash",
                Credits = buyerCredits,
            });
        await db.SaveChangesAsync();
        return (sellerId, buyerId);
    }
}
