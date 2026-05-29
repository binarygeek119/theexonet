using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;
using Rava.Infrastructure.Mapping;

namespace Rava.Infrastructure.Services;

public class PlayerGameService(
    AppDbContext db,
    IMineSimulationService simulation,
    IMarketDataProvider marketProvider,
    IStarterMineGenerator starterMineGenerator,
    IPasswordHasher passwordHasher)
{
    public async Task<(PlayerEntity? Player, MineEntity? Mine, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (await db.Players.AnyAsync(p => p.Username == request.Username || p.Email == request.Email, ct))
        {
            return (null, null, "Username or email already exists.");
        }

        var playerId = Guid.NewGuid();
        var asteroidSeed = Random.Shared.Next(1000, 999999);
        var (mineState, starterInventory) = starterMineGenerator.Generate(playerId, asteroidSeed);

        var player = new PlayerEntity
        {
            Id = playerId,
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Credits = GameBalance.StarterCredits,
            CurrentGameDay = 1
        };

        var mine = EntityMapper.ToEntity(mineState, playerId);
        var inventoryEntities = starterInventory.Select(EntityMapper.ToEntity).ToList();

        player.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = TransactionType.StarterGrant,
            Amount = GameBalance.StarterCredits,
            Description = "Starter credits grant",
            GameDay = 1
        });

        db.Players.Add(player);
        db.Mines.Add(mine);
        db.Inventory.AddRange(inventoryEntities);
        await db.SaveChangesAsync(ct);

        return (player, mine, null);
    }

    public async Task<(PlayerEntity? Player, string? Error)> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Username == request.Username, ct);
        if (player is null || !passwordHasher.Verify(request.Password, player.PasswordHash))
        {
            return (null, "Invalid username or password.");
        }

        return (player, null);
    }

    public async Task<MineDetailResponse?> GetMineAsync(Guid playerId, Guid mineId, CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return null;
        }

        var inventory = await db.Inventory.AsNoTracking()
            .Where(i => i.PlayerId == playerId).ToListAsync(ct);

        return MapMineDetail(player, mine, inventory);
    }

    public async Task<(bool Success, string Message)> AssignWorkerAsync(
        Guid playerId, Guid mineId, AssignWorkerRequest request, CancellationToken ct)
    {
        var mine = await LoadMineAsync(mineId, ct);
        if (mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.");
        }

        var worker = mine.Workers.FirstOrDefault(w => w.Id == request.WorkerId);
        if (worker is null)
        {
            return (false, "Worker not found.");
        }

        if (!string.IsNullOrEmpty(request.ZoneId))
        {
            if (!Guid.TryParse(request.ZoneId, out var zoneGuid))
            {
                return (false, "Invalid zone id.");
            }

            var zone = mine.Zones.FirstOrDefault(z => z.Id == zoneGuid);
            if (zone is null)
            {
                return (false, "Zone not found.");
            }

            if (zone.DepletedPct >= 100m && !zone.IsSalvageZone)
            {
                return (false, "Zone is fully depleted.");
            }

            worker.AssignedZoneId = zoneGuid;
        }
        else
        {
            worker.AssignedZoneId = null;
        }

        await db.SaveChangesAsync(ct);
        return (true, request.ZoneId != null ? "Worker assigned to zone." : "Worker unassigned.");
    }

    public async Task<(bool Success, string Message, decimal? NewCredits)> BuySupplyAsync(
        Guid playerId, Guid mineId, BuySupplyRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.", null);
        }

        if (request.Quantity <= 0)
        {
            return (false, "Quantity must be positive.", null);
        }

        var supplyType = (SupplyType)request.SupplyType;
        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, ct);
        var unitPrice = market.Prices.First(p => p.SupplyType == supplyType).Price;
        var totalCost = Math.Round(unitPrice * request.Quantity, 2);

        if (player.Credits < totalCost)
        {
            return (false, "Insufficient credits.", player.Credits);
        }

        var item = await db.Inventory.FirstOrDefaultAsync(i =>
            i.PlayerId == playerId &&
            i.Category == ItemCategory.Supply &&
            i.ItemType == supplyType.ToString(), ct);

        if (item is null)
        {
            item = new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Category = ItemCategory.Supply,
                ItemType = supplyType.ToString(),
                Quantity = request.Quantity
            };
            db.Inventory.Add(item);
        }
        else
        {
            item.Quantity += request.Quantity;
        }

        player.Credits -= totalCost;
        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = TransactionType.SupplyPurchase,
            Amount = -totalCost,
            Description = $"Purchased {request.Quantity} {supplyType}",
            GameDay = player.CurrentGameDay
        });

        await db.SaveChangesAsync(ct);
        return (true, $"Purchased {request.Quantity} {supplyType} for {totalCost} credits.", player.Credits);
    }

    public async Task<(bool Success, string Message, decimal? NewCredits)> SellOreAsync(
        Guid playerId, Guid mineId, SellOreRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.", null);
        }

        if (request.Quantity <= 0)
        {
            return (false, "Quantity must be positive.", null);
        }

        var oreType = (OreType)request.OreType;
        var item = await db.Inventory.FirstOrDefaultAsync(i =>
            i.PlayerId == playerId &&
            i.Category == ItemCategory.Ore &&
            i.ItemType == oreType.ToString(), ct);

        if (item is null || item.Quantity < request.Quantity)
        {
            return (false, "Insufficient ore in inventory.", player.Credits);
        }

        var basePrice = GameBalance.BaseOrePrices[oreType];
        var rate = request.EmergencyBuyback ? GameBalance.EmergencyBuybackRate : 1m;
        var totalValue = Math.Round(basePrice * request.Quantity * rate, 2);

        item.Quantity -= request.Quantity;
        player.Credits += totalValue;

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = request.EmergencyBuyback ? TransactionType.EmergencyBuyback : TransactionType.OreSale,
            Amount = totalValue,
            Description = request.EmergencyBuyback
                ? $"Emergency buyback: {request.Quantity} {oreType} at 50%"
                : $"Sold {request.Quantity} {oreType}",
            GameDay = player.CurrentGameDay
        });

        await db.SaveChangesAsync(ct);
        return (true, $"Sold {request.Quantity} {oreType} for {totalValue} credits.", player.Credits);
    }

    public async Task<DayAdvanceResponse?> AdvanceDayAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        var mine = await LoadPlayerMineAsync(playerId, ct, activeOnly: true);
        if (mine is null)
        {
            return null;
        }

        var inventory = await db.Inventory.Where(i => i.PlayerId == playerId).ToListAsync(ct);
        var inventoryStates = inventory.Select(EntityMapper.ToState).ToList();

        var nextDay = player.CurrentGameDay + 1;
        var market = await GetOrCreateMarketSnapshotAsync(nextDay, ct);

        var playerState = EntityMapper.ToState(player);
        var mineState = EntityMapper.ToState(mine);
        var result = simulation.AdvanceDay(playerState, mineState, inventoryStates, market);

        player.Credits = playerState.Credits;
        player.CurrentGameDay = playerState.CurrentGameDay;

        foreach (var zoneState in mineState.Zones)
        {
            var zoneEntity = mine.Zones.First(z => z.Id == zoneState.Id);
            EntityMapper.ApplyZone(zoneEntity, zoneState);
        }

        foreach (var state in inventoryStates)
        {
            var entity = inventory.FirstOrDefault(e =>
                e.Category == state.Category && e.ItemType == state.ItemType);

            if (entity is null)
            {
                db.Inventory.Add(EntityMapper.ToEntity(state));
            }
            else
            {
                entity.Quantity = state.Quantity;
            }
        }

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = TransactionType.Payroll,
            Amount = -result.PayrollPaid,
            Description = "Daily payroll",
            GameDay = result.NewGameDay
        });

        if (result.SuppliesConsumed > 0)
        {
            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.DayAdvance,
                Amount = -result.SuppliesConsumed,
                Description = "Daily supply consumption",
                GameDay = result.NewGameDay
            });
        }

        var world = await db.GameWorld.FirstAsync(ct);
        world.CurrentDay = Math.Max(world.CurrentDay, result.NewGameDay);
        world.LastTickAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new DayAdvanceResponse(
            result.NewGameDay,
            result.Credits,
            result.OreExtracted.Select(kv => new OreExtractedDto(kv.Key, kv.Value)).ToList(),
            result.PayrollPaid,
            result.SuppliesConsumed,
            MapMarket(result.MarketSnapshot),
            result.Messages);
    }

    public async Task<MarketTodayResponse?> GetMarketTodayAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, ct);
        return MapMarket(market);
    }

    public async Task<FinanceResponse?> GetFinancesAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        var mine = await LoadPlayerMineAsync(playerId, ct, activeOnly: true);
        if (mine is null)
        {
            return null;
        }

        var inventory = await db.Inventory.AsNoTracking()
            .Where(i => i.PlayerId == playerId).ToListAsync(ct);
        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => t.PlayerId == playerId).ToListAsync(ct);

        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, ct);
        var summary = simulation.BuildFinanceSummary(
            EntityMapper.ToState(player),
            EntityMapper.ToState(mine),
            inventory.Select(EntityMapper.ToState).ToList(),
            transactions.Select(EntityMapper.ToState).ToList(),
            market);

        return new FinanceResponse(
            summary.Credits,
            summary.DailyPayroll,
            summary.DailySupplyCost,
            summary.EstimatedDailyIncome,
            summary.RunwayDays,
            summary.IsSoftlocked,
            summary.CanEmergencyBuyback,
            summary.RecentTransactions.Select(t => new TransactionDto(
                t.Type.ToString(), t.Amount, t.Description, t.GameDay, t.CreatedAt)).ToList());
    }

    public async Task<Guid?> GetPrimaryMineIdAsync(Guid playerId, CancellationToken ct)
    {
        var mineId = await db.Mines
            .Where(m => m.PlayerId == playerId && m.Status == MineStatus.Active)
            .OrderBy(m => m.PurchasedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        return mineId;
    }

    private async Task<MineEntity?> LoadMineAsync(Guid mineId, CancellationToken ct, bool activeOnly = false)
    {
        var query = db.Mines
            .Include(m => m.Zones)
            .Include(m => m.Workers)
            .Where(m => m.Id == mineId);

        if (activeOnly)
        {
            query = query.Where(m => m.Status == MineStatus.Active);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<MineEntity?> LoadPlayerMineAsync(Guid playerId, CancellationToken ct, bool activeOnly)
    {
        var query = db.Mines
            .Include(m => m.Zones)
            .Include(m => m.Workers)
            .Where(m => m.PlayerId == playerId);

        if (activeOnly)
        {
            query = query.Where(m => m.Status == MineStatus.Active);
        }

        return await query.OrderBy(m => m.PurchasedAt).FirstOrDefaultAsync(ct);
    }

    private async Task<DailyMarketSnapshot> GetOrCreateMarketSnapshotAsync(int gameDay, CancellationToken ct)
    {
        var existing = await db.MarketPriceHistory.AsNoTracking()
            .Where(m => m.GameDay == gameDay)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            return new DailyMarketSnapshot
            {
                GameDay = gameDay,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = existing[0].Source,
                Prices = existing.Select(e => new MarketPriceEntry
                {
                    SupplyType = e.SupplyType,
                    Price = e.Price,
                    ChangePct = 0
                }).ToList()
            };
        }

        var world = await db.GameWorld.FirstAsync(ct);
        var snapshot = await marketProvider.GetDailyPricesAsync(gameDay, world.MarketSeed, ct);

        foreach (var price in snapshot.Prices)
        {
            db.MarketPriceHistory.Add(new MarketPriceHistoryEntity
            {
                Id = Guid.NewGuid(),
                GameDay = gameDay,
                SupplyType = price.SupplyType,
                Price = price.Price,
                Source = snapshot.Source
            });
        }

        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    private static MineDetailResponse MapMineDetail(
        PlayerEntity player, MineEntity mine, List<InventoryItemEntity> inventory) =>
        new(
            mine.Id,
            mine.Name,
            mine.AsteroidSeed,
            mine.Status.ToString(),
            player.CurrentGameDay,
            player.Credits,
            mine.Zones.Select(z => new MineZoneDto(
                z.Id, z.X, z.Y, (OreTypeDto)z.OreType, z.Richness, z.DepletedPct, z.IsSalvageZone)).ToList(),
            mine.Workers.Select(w => new WorkerDto(w.Id, w.Name, w.Skill, w.Salary, w.AssignedZoneId)).ToList(),
            inventory.Select(i => new InventoryItemDto(i.ItemType, i.Category.ToString(), i.Quantity)).ToList(),
            FeatureFlags.Phase1);

    private static MarketTodayResponse MapMarket(DailyMarketSnapshot market) =>
        new(
            market.GameDay,
            market.Prices.Select(p => new MarketPriceDto(
                (SupplyTypeDto)p.SupplyType, p.Price, p.ChangePct)).ToList(),
            market.Source);
}
