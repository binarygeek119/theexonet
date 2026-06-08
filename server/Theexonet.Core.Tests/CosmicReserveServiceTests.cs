using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Core.Tests;

public class CosmicReserveServiceTests
{
    private sealed class NoOpBroadcaster : ILiveUpdateBroadcaster
    {
        public IAsyncEnumerable<LiveUpdateEventDto> SubscribeAsync(Guid playerId, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<LiveUpdateEventDto>();

        public void PublishToPlayer(Guid playerId, LiveUpdateEventDto evt) { }

        public void PublishGlobal(LiveUpdateEventDto evt) { }
    }

    private static CosmicReserveService CreateService(AppDbContext db)
    {
        var simulation = new MineSimulationService(MarketItemsCatalog.CreateDefault());
        return new CosmicReserveService(db, simulation, new NoOpBroadcaster());
    }

    private static AppDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void ResolveDailyJobSalary_ReturnsCatalogValueForAsteroidMiner()
    {
        var salary = CosmicReserveService.ResolveDailyJobSalary(PlayerJobCatalog.AsteroidMiner);
        Assert.Equal(GameBalance.AsteroidMinerDailySalary, salary);
    }

    [Fact]
    public void ResolveDailyJobSalary_ReturnsZeroForUnknownJob()
    {
        Assert.Equal(0m, CosmicReserveService.ResolveDailyJobSalary("lunar_barista"));
    }

    [Fact]
    public async Task TransferAsync_MovesRaxToOperating()
    {
        await using var db = CreateDb(nameof(TransferAsync_MovesRaxToOperating));
        var playerId = Guid.NewGuid();
        db.Players.Add(new PlayerEntity
        {
            Id = playerId,
            Username = "banker",
            Email = "banker@test",
            PasswordHash = "hash",
            Credits = 500m,
            ReserveBalance = 2000m,
            CurrentGameDay = 3,
            LastProcessedUtcDate = new DateOnly(2026, 6, 8),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.TransferAsync(
            playerId,
            new ReserveTransferRequest(250m, "to_operating"),
            CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(1750m, result!.ReserveBalance);
        Assert.Equal(750m, result.OperatingBalance);

        var player = await db.Players.SingleAsync(p => p.Id == playerId);
        Assert.Equal(1750m, player.ReserveBalance);
        Assert.Equal(750m, player.Credits);
    }

    [Fact]
    public async Task TransferAsync_RejectsInsufficientReserve()
    {
        await using var db = CreateDb(nameof(TransferAsync_RejectsInsufficientReserve));
        var playerId = Guid.NewGuid();
        db.Players.Add(new PlayerEntity
        {
            Id = playerId,
            Username = "broke",
            Email = "broke@test",
            PasswordHash = "hash",
            Credits = 100m,
            ReserveBalance = 50m,
            CurrentGameDay = 1,
            LastProcessedUtcDate = new DateOnly(2026, 6, 8),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (result, error) = await service.TransferAsync(
            playerId,
            new ReserveTransferRequest(100m, "to_operating"),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal("Insufficient Cosmic Reserve balance.", error);
    }
}
