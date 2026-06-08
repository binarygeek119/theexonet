using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class CompanyCrewService(
    AppDbContext db,
    ILiveUpdateBroadcaster liveUpdateBroadcaster)
{
    private static readonly string[] HireableNames =
    [
        "Juno Hale", "Kaito Ren", "Sera Okoye", "Tamsin Reed", "Viktor Nash",
        "Yara Quinn", "Zane Mercer", "Piper Sol", "Orion Tate", "Nia Calder",
        "Elias Boone", "Freya Dunn", "Garrick Poe", "Hana Voss", "Ivo Crane",
    ];

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> HireWorkerAsync(
        Guid playerId,
        Guid mineId,
        HireWorkerRequest? request,
        CancellationToken ct)
    {
        var (player, mine, error) = await LoadOwnedMineAsync(playerId, mineId, ct);
        if (error is not null)
        {
            return (false, error, []);
        }

        if (mine!.Workers.Count >= GameBalance.MaxMineWorkers)
        {
            return (false, $"Worker cap reached ({GameBalance.MaxMineWorkers}).", []);
        }

        if (player!.ReserveBalance < GameBalance.HireFee)
        {
            return (false, "Insufficient Cosmic Reserve balance for hiring fee.", []);
        }

        var rng = new Random();
        var existingNames = mine.Workers.Select(w => w.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = string.IsNullOrWhiteSpace(request?.Name)
            ? PickRandomName(existingNames, rng)
            : request.Name.Trim();

        if (existingNames.Contains(name))
        {
            return (false, "A worker with that name already exists on this crew.", []);
        }

        var skill = GameBalance.HireSkillMin + rng.Next(
            GameBalance.HireSkillMax - GameBalance.HireSkillMin + 1);
        var salary = GameBalance.HireSalaryMin
            + (decimal)rng.NextDouble() * (GameBalance.HireSalaryMax - GameBalance.HireSalaryMin);
        salary = Math.Round(salary, 0);

        player.ReserveBalance -= GameBalance.HireFee;
        db.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = ReserveTransactionType.HireFee,
            Amount = -GameBalance.HireFee,
            Description = $"Hired {name}",
            GameDay = player.CurrentGameDay,
        });

        mine.Workers.Add(new WorkerEntity
        {
            Id = Guid.NewGuid(),
            MineId = mine.Id,
            Name = name,
            Skill = skill,
            Salary = salary,
        });

        await db.SaveChangesAsync(ct);
        NotifyRefresh(playerId);
        return (true, $"Hired {name} (skill {skill}, {salary:0} Rax/day).", []);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> FireWorkerAsync(
        Guid playerId,
        Guid mineId,
        Guid workerId,
        CancellationToken ct)
    {
        var (player, mine, error) = await LoadOwnedMineAsync(playerId, mineId, ct);
        if (error is not null)
        {
            return (false, error, []);
        }

        var worker = mine!.Workers.FirstOrDefault(w => w.Id == workerId);
        if (worker is null)
        {
            return (false, "Worker not found.", []);
        }

        var severance = GameBalance.FireSeverance(worker.Salary);
        if (player!.ReserveBalance < severance)
        {
            return (false, "Insufficient Cosmic Reserve balance for fire severance.", []);
        }

        player.ReserveBalance -= severance;
        db.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = ReserveTransactionType.FireSeverance,
            Amount = -severance,
            Description = $"Fired {worker.Name}",
            GameDay = player.CurrentGameDay,
        });

        db.Workers.Remove(worker);
        await db.SaveChangesAsync(ct);
        NotifyRefresh(playerId);
        return (true, $"Fired {worker.Name}. Severance: {severance:0} Rax.", []);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> LayoffWorkerAsync(
        Guid playerId,
        Guid mineId,
        Guid workerId,
        CancellationToken ct)
    {
        var (player, mine, error) = await LoadOwnedMineAsync(playerId, mineId, ct);
        if (error is not null)
        {
            return (false, error, []);
        }

        var worker = mine!.Workers.FirstOrDefault(w => w.Id == workerId);
        if (worker is null)
        {
            return (false, "Worker not found.", []);
        }

        var severance = GameBalance.LayoffSeverance(worker.Salary);
        if (player!.ReserveBalance < severance)
        {
            return (false, "Insufficient Cosmic Reserve balance for layoff severance.", []);
        }

        player.ReserveBalance -= severance;
        db.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = ReserveTransactionType.LayoffSeverance,
            Amount = -severance,
            Description = $"Laid off {worker.Name}",
            GameDay = player.CurrentGameDay,
        });

        db.Workers.Remove(worker);
        await db.SaveChangesAsync(ct);
        NotifyRefresh(playerId);
        return (true, $"Laid off {worker.Name}. Severance: {severance:0} Rax.", []);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> RaiseWorkerAsync(
        Guid playerId,
        Guid mineId,
        Guid workerId,
        RaiseWorkerRequest request,
        CancellationToken ct)
    {
        var (_, mine, error) = await LoadOwnedMineAsync(playerId, mineId, ct);
        if (error is not null)
        {
            return (false, error, []);
        }

        var worker = mine!.Workers.FirstOrDefault(w => w.Id == workerId);
        if (worker is null)
        {
            return (false, "Worker not found.", []);
        }

        if (request.NewSalary <= worker.Salary)
        {
            return (false, "Raise must increase salary.", []);
        }

        if (request.NewSalary > GameBalance.MaxWorkerSalary)
        {
            return (false, $"Maximum salary is {GameBalance.MaxWorkerSalary:0} Rax/day.", []);
        }

        worker.Salary = Math.Round(request.NewSalary, 2);
        await db.SaveChangesAsync(ct);
        NotifyRefresh(playerId);
        return (true, $"{worker.Name} salary raised to {worker.Salary:0} Rax/day.", []);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> RenewMiningRightsAsync(
        Guid playerId,
        Guid mineId,
        CancellationToken ct)
    {
        var (player, mine, error) = await LoadOwnedMineAsync(playerId, mineId, ct);
        if (error is not null)
        {
            return (false, error, []);
        }

        if (player!.ReserveBalance < GameBalance.MiningRightsRenewalFee)
        {
            return (false, "Insufficient Cosmic Reserve balance for mining rights renewal.", []);
        }

        var baseDay = Math.Max(player.CurrentGameDay, mine!.MiningRightsPaidThroughDay);
        mine.MiningRightsPaidThroughDay = baseDay + GameBalance.MiningRightsPeriodDays;

        player.ReserveBalance -= GameBalance.MiningRightsRenewalFee;
        db.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = ReserveTransactionType.MiningRights,
            Amount = -GameBalance.MiningRightsRenewalFee,
            Description = "Mining rights renewal",
            GameDay = player.CurrentGameDay,
        });

        await db.SaveChangesAsync(ct);
        NotifyRefresh(playerId);
        return (true, $"Mining rights renewed through day {mine.MiningRightsPaidThroughDay}.", []);
    }

    private async Task<(PlayerEntity? Player, MineEntity? Mine, string? Error)> LoadOwnedMineAsync(
        Guid playerId,
        Guid mineId,
        CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null, "Player not found.");
        }

        var mine = await db.Mines
            .Include(m => m.Workers)
            .FirstOrDefaultAsync(m => m.Id == mineId && m.PlayerId == playerId && m.Status == MineStatus.Active, ct);

        if (mine is null)
        {
            return (null, null, "Mine not found.");
        }

        return (player, mine, null);
    }

    private static string PickRandomName(HashSet<string> taken, Random rng)
    {
        var available = HireableNames.Where(n => !taken.Contains(n)).ToList();
        if (available.Count > 0)
        {
            return available[rng.Next(available.Count)];
        }

        return $"Miner {rng.Next(1000, 9999)}";
    }

    private void NotifyRefresh(Guid playerId)
    {
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Reserve);
    }
}
