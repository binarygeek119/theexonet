using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class MineSimulationServiceTests
{
    private readonly IMarketItemsCatalog _marketItems = MarketItemsCatalog.CreateDefault();
    private readonly IMineSimulationService _simulation;
    private readonly IMarketDataProvider _market;

    public MineSimulationServiceTests()
    {
        _simulation = new MineSimulationService(_marketItems);
        _market = new MockMarketGenerator(_marketItems);
    }

    [Fact]
    public async Task AdvanceDay_ExtractsOre_WhenWorkerAssigned()
    {
        var player = CreatePlayer();
        var mine = CreateMineWithAssignedWorker();
        var inventory = CreateStarterInventory(player.Id);
        var market = await _market.GetDailyPricesAsync(1, new DateOnly(2026, 5, 29));

        var result = _simulation.AdvanceDay(player, mine, inventory, market);

        Assert.True(result.OreExtracted.Count > 0);
        Assert.Contains(inventory, i => i.Category == ItemCategory.Ore && i.Quantity > 0);
    }

    [Fact]
    public async Task AdvanceDay_DeductsPayroll()
    {
        var player = CreatePlayer();
        var mine = CreateMineWithAssignedWorker();
        var inventory = CreateStarterInventory(player.Id);
        var market = await _market.GetDailyPricesAsync(1, new DateOnly(2026, 5, 29));
        var startingCredits = player.Credits;

        var result = _simulation.AdvanceDay(player, mine, inventory, market);

        Assert.Equal(result.PayrollPaid, mine.Workers.Sum(w => w.Salary));
        Assert.True(player.Credits < startingCredits);
    }

    [Fact]
    public void IsSoftlocked_ReturnsTrue_WhenBrokeWithNoOre()
    {
        var player = CreatePlayer();
        player.Credits = 0;
        var inventory = CreateStarterInventory(player.Id);

        Assert.True(_simulation.IsSoftlocked(player, inventory));
    }

    [Fact]
    public void IsSoftlocked_ReturnsFalse_WhenBrokeButHasOre()
    {
        var player = CreatePlayer();
        player.Credits = 0;
        var inventory = CreateStarterInventory(player.Id);
        inventory.Add(new InventoryItemState
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Category = ItemCategory.Ore,
            ItemType = OreType.SalvageScrap.ToString(),
            Quantity = 5
        });

        Assert.False(_simulation.IsSoftlocked(player, inventory));
    }

    [Fact]
    public async Task MockMarket_GeneratesPricesForAllSupplies()
    {
        var snapshot = await _market.GetDailyPricesAsync(3, new DateOnly(2026, 5, 29));

        Assert.Equal(4, snapshot.Prices.Count);
        Assert.All(snapshot.Prices, p => Assert.True(p.Price > 0));
    }

    [Fact]
    public void StarterMineGenerator_CreatesGridAndWorkers()
    {
        var generator = new StarterMineGenerator();
        var (mine, inventory) = generator.Generate(Guid.NewGuid(), 12345);

        Assert.Equal(GameBalance.GridSize * GameBalance.GridSize, mine.Zones.Count);
        Assert.Equal(GameBalance.StarterWorkerCount, mine.Workers.Count);
        Assert.Contains(mine.Zones, z => z.IsSalvageZone);
        Assert.Equal(4, inventory.Count);
    }

    private static PlayerState CreatePlayer() => new()
    {
        Id = Guid.NewGuid(),
        Username = "test",
        Credits = GameBalance.StarterCredits,
        CurrentGameDay = 1
    };

    private static MineState CreateMineWithAssignedWorker()
    {
        var generator = new StarterMineGenerator();
        var (mine, _) = generator.Generate(Guid.NewGuid(), 999);
        var zone = mine.Zones.First(z => !z.IsSalvageZone);
        mine.Workers[0].AssignedZoneId = zone.Id;
        return mine;
    }

    private static List<InventoryItemState> CreateStarterInventory(Guid playerId)
    {
        var generator = new StarterMineGenerator();
        var (_, inventory) = generator.Generate(playerId, 999);
        return inventory;
    }
}
