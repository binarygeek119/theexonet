using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;
using Theexonet.Infrastructure.Mapping;

namespace Theexonet.Infrastructure.Services;

public class CosmicReserveService(
    AppDbContext db,
    IMineSimulationService simulation,
    ILiveUpdateBroadcaster liveUpdateBroadcaster)
{
    public async Task<CosmicReserveResponse?> GetAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        var mine = await db.Mines.AsNoTracking()
            .Include(m => m.Workers)
            .Where(m => m.PlayerId == playerId && m.Status == MineStatus.Active)
            .FirstOrDefaultAsync(ct);

        var currentJob = await db.PlayerJobHistory.AsNoTracking()
            .Where(j => j.PlayerId == playerId && j.IsCurrent)
            .Select(j => new { j.JobSlug, j.JobTitle })
            .FirstOrDefaultAsync(ct);

        var dailyJobSalary = ResolveDailyJobSalary(currentJob?.JobSlug);
        var dailyMinePayroll = mine is null ? 0m : simulation.CalculateDailyPayroll(EntityMapper.ToState(mine));

        var reserveTransactions = await db.ReserveTransactions.AsNoTracking()
            .Where(t => t.PlayerId == playerId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return new CosmicReserveResponse(
            player.ReserveBalance,
            player.Credits,
            dailyJobSalary,
            dailyMinePayroll,
            currentJob?.JobTitle,
            reserveTransactions.Select(t => new ReserveTransactionDto(
                t.Type.ToString(),
                t.Amount,
                t.Description,
                t.GameDay,
                t.CreatedAt)).ToList());
    }

    public async Task<(CosmicReserveResponse? Result, string? Error)> TransferAsync(
        Guid playerId,
        ReserveTransferRequest request,
        CancellationToken ct)
    {
        if (request.Amount <= 0)
        {
            return (null, "Transfer amount must be greater than zero.");
        }

        var direction = request.Direction?.Trim().ToLowerInvariant();
        if (direction is not ("to_operating" or "to_reserve"))
        {
            return (null, "Direction must be to_operating or to_reserve.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, "Player not found.");
        }

        if (direction == "to_operating")
        {
            if (player.ReserveBalance < request.Amount)
            {
                return (null, "Insufficient Cosmic Reserve balance.");
            }

            player.ReserveBalance -= request.Amount;
            player.Credits += request.Amount;

            db.ReserveTransactions.Add(new ReserveTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = ReserveTransactionType.TransferToOperating,
                Amount = -request.Amount,
                Description = "Transfer to operating float",
                GameDay = player.CurrentGameDay
            });

            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.TransferFromReserve,
                Amount = request.Amount,
                Description = "Transfer from Cosmic Reserve",
                GameDay = player.CurrentGameDay
            });
        }
        else
        {
            if (player.Credits < request.Amount)
            {
                return (null, "Insufficient operating balance.");
            }

            player.Credits -= request.Amount;
            player.ReserveBalance += request.Amount;

            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.TransferToReserve,
                Amount = -request.Amount,
                Description = "Transfer to Cosmic Reserve",
                GameDay = player.CurrentGameDay
            });

            db.ReserveTransactions.Add(new ReserveTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = ReserveTransactionType.TransferFromOperating,
                Amount = request.Amount,
                Description = "Transfer from operating float",
                GameDay = player.CurrentGameDay
            });
        }

        await db.SaveChangesAsync(ct);
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Reserve);
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);

        return (await GetAsync(playerId, ct), null);
    }

    public static decimal ResolveDailyJobSalary(string? jobSlug) =>
        PlayerJobCatalog.TryGet(jobSlug)?.DailySalary ?? 0m;
}
