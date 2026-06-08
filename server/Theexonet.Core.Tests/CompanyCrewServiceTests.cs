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

public class CompanyCrewServiceTests
{
    private sealed class NoOpBroadcaster : ILiveUpdateBroadcaster
    {
        public IAsyncEnumerable<LiveUpdateEventDto> SubscribeAsync(Guid playerId, CancellationToken cancellationToken) =>
            AsyncEnumerable.Empty<LiveUpdateEventDto>();

        public void PublishToPlayer(Guid playerId, LiveUpdateEventDto evt) { }

        public void PublishGlobal(LiveUpdateEventDto evt) { }
    }

    private static CompanyCrewService CreateService(AppDbContext db) =>
        new(db, new NoOpBroadcaster());

    private static AppDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task FireWorker_ChargesHigherSeveranceThanLayoff()
    {
        await using var db = CreateDb(nameof(FireWorker_ChargesHigherSeveranceThanLayoff));
        var (playerId, mineId, workerId, salary) = await SeedCrewAsync(db, reserve: 5000m);

        var service = CreateService(db);
        var layoffReserveBefore = (await db.Players.SingleAsync(p => p.Id == playerId)).ReserveBalance;
        await service.LayoffWorkerAsync(playerId, mineId, workerId, CancellationToken.None);
        var afterLayoff = (await db.Players.SingleAsync(p => p.Id == playerId)).ReserveBalance;
        var layoffCost = layoffReserveBefore - afterLayoff;

        var (playerId2, mineId2, workerId2, salary2) = await SeedCrewAsync(db, reserve: 5000m, suffix: "b");
        var fireReserveBefore = (await db.Players.SingleAsync(p => p.Id == playerId2)).ReserveBalance;
        await service.FireWorkerAsync(playerId2, mineId2, workerId2, CancellationToken.None);
        var afterFire = (await db.Players.SingleAsync(p => p.Id == playerId2)).ReserveBalance;
        var fireCost = fireReserveBefore - afterFire;

        Assert.Equal(GameBalance.LayoffSeverance(salary), layoffCost);
        Assert.Equal(GameBalance.FireSeverance(salary2), fireCost);
        Assert.True(fireCost > layoffCost);
    }

    [Fact]
    public async Task HireWorker_RejectsWhenReserveTooLow()
    {
        await using var db = CreateDb(nameof(HireWorker_RejectsWhenReserveTooLow));
        var (playerId, mineId, _, _) = await SeedCrewAsync(db, reserve: 50m);

        var service = CreateService(db);
        var (success, message, _) = await service.HireWorkerAsync(playerId, mineId, null, CancellationToken.None);

        Assert.False(success);
        Assert.Contains("Insufficient", message);
    }

    [Fact]
    public async Task RaiseWorker_RejectsDecrease()
    {
        await using var db = CreateDb(nameof(RaiseWorker_RejectsDecrease));
        var (playerId, mineId, workerId, salary) = await SeedCrewAsync(db, reserve: 1000m);

        var service = CreateService(db);
        var (success, _, _) = await service.RaiseWorkerAsync(
            playerId,
            mineId,
            workerId,
            new RaiseWorkerRequest(salary - 10m),
            CancellationToken.None);

        Assert.False(success);
    }

    private static async Task<(Guid PlayerId, Guid MineId, Guid WorkerId, decimal Salary)> SeedCrewAsync(
        AppDbContext db,
        decimal reserve,
        string suffix = "")
    {
        var playerId = Guid.NewGuid();
        var mineId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        const decimal salary = 120m;

        db.Players.Add(new PlayerEntity
        {
            Id = playerId,
            Username = $"crew{suffix}",
            Email = $"crew{suffix}@test",
            PasswordHash = "hash",
            Credits = 500m,
            ReserveBalance = reserve,
            CurrentGameDay = 5,
            LastProcessedUtcDate = new DateOnly(2026, 6, 8),
        });

        db.Mines.Add(new MineEntity
        {
            Id = mineId,
            PlayerId = playerId,
            Name = $"Mine {suffix}",
            AsteroidSeed = 123,
            Status = MineStatus.Active,
            MiningRightsPaidThroughDay = 40,
            Workers =
            [
                new WorkerEntity
                {
                    Id = workerId,
                    MineId = mineId,
                    Name = $"Worker {suffix}",
                    Skill = 2,
                    Salary = salary,
                },
            ],
        });

        await db.SaveChangesAsync();
        return (playerId, mineId, workerId, salary);
    }
}
