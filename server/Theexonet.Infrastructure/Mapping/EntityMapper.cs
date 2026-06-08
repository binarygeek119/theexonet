using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Mapping;

public static class EntityMapper
{
    public static PlayerState ToState(PlayerEntity entity) => new()
    {
        Id = entity.Id,
        Username = entity.Username,
        Email = entity.Email,
        PasswordHash = entity.PasswordHash,
        Credits = entity.Credits,
        ReserveBalance = entity.ReserveBalance,
        CurrentGameDay = entity.CurrentGameDay,
        CreatedAt = entity.CreatedAt
    };

    public static MineState ToState(MineEntity entity) => new()
    {
        Id = entity.Id,
        PlayerId = entity.PlayerId,
        Name = entity.Name,
        AsteroidSeed = entity.AsteroidSeed,
        Status = entity.Status,
        PurchasedAt = entity.PurchasedAt,
        MiningRightsPaidThroughDay = entity.MiningRightsPaidThroughDay,
        Zones = entity.Zones.Select(ToState).ToList(),
        Workers = entity.Workers.Select(ToState).ToList()
    };

    public static MineZoneState ToState(MineZoneEntity entity) => new()
    {
        Id = entity.Id,
        MineId = entity.MineId,
        X = entity.X,
        Y = entity.Y,
        OreType = entity.OreType,
        Richness = entity.Richness,
        DepletedPct = entity.DepletedPct,
        IsSalvageZone = entity.IsSalvageZone
    };

    public static WorkerState ToState(WorkerEntity entity) => new()
    {
        Id = entity.Id,
        MineId = entity.MineId,
        Name = entity.Name,
        Skill = entity.Skill,
        Salary = entity.Salary,
        AssignedZoneId = entity.AssignedZoneId
    };

    public static InventoryItemState ToState(InventoryItemEntity entity) => new()
    {
        Id = entity.Id,
        PlayerId = entity.PlayerId,
        Category = entity.Category,
        ItemType = entity.ItemType,
        Quantity = entity.Quantity,
        Condition = entity.Condition,
        BrokenQuantity = entity.BrokenQuantity,
        IsNew = entity.IsNew,
    };

    public static TransactionState ToState(TransactionEntity entity) => new()
    {
        Id = entity.Id,
        PlayerId = entity.PlayerId,
        Type = entity.Type,
        Amount = entity.Amount,
        Description = entity.Description,
        GameDay = entity.GameDay,
        CreatedAt = entity.CreatedAt
    };

    public static ReserveTransactionState ToState(ReserveTransactionEntity entity) => new()
    {
        Id = entity.Id,
        PlayerId = entity.PlayerId,
        Type = entity.Type,
        Amount = entity.Amount,
        Description = entity.Description,
        GameDay = entity.GameDay,
        CreatedAt = entity.CreatedAt
    };

    public static void ApplyZone(MineZoneEntity entity, MineZoneState state)
    {
        entity.DepletedPct = state.DepletedPct;
    }

    public static void ApplyWorker(WorkerEntity entity, WorkerState state)
    {
        entity.AssignedZoneId = state.AssignedZoneId;
    }

    public static void ApplyInventory(InventoryItemEntity entity, InventoryItemState state)
    {
        entity.Quantity = state.Quantity;
        entity.Condition = state.Condition;
        entity.BrokenQuantity = state.BrokenQuantity;
        entity.IsNew = state.IsNew;
    }

    public static MineStockpileState ToState(MineOreStockpileEntity entity) => new()
    {
        Id = entity.Id,
        MineId = entity.MineId,
        OreType = entity.OreType,
        Quantity = entity.Quantity,
        Condition = entity.Condition,
    };

    public static void ApplyStockpile(MineOreStockpileEntity entity, MineStockpileState state)
    {
        entity.Quantity = state.Quantity;
        entity.Condition = state.Condition;
    }

    public static MineOreStockpileEntity ToEntity(MineStockpileState state) => new()
    {
        Id = state.Id,
        MineId = state.MineId,
        OreType = state.OreType,
        Quantity = state.Quantity,
        Condition = state.Condition,
    };

    public static InventoryItemEntity ToEntity(InventoryItemState state) => new()
    {
        Id = state.Id,
        PlayerId = state.PlayerId,
        Category = state.Category,
        ItemType = state.ItemType,
        Quantity = state.Quantity,
        Condition = state.Condition,
        BrokenQuantity = state.BrokenQuantity,
        IsNew = state.IsNew,
    };

    public static MineEntity ToEntity(MineState state, Guid playerId) => new()
    {
        Id = state.Id,
        PlayerId = playerId,
        Name = state.Name,
        AsteroidSeed = state.AsteroidSeed,
        Status = state.Status,
        PurchasedAt = state.PurchasedAt,
        MiningRightsPaidThroughDay = state.MiningRightsPaidThroughDay,
        Zones = state.Zones.Select(z => new MineZoneEntity
        {
            Id = z.Id,
            MineId = state.Id,
            X = z.X,
            Y = z.Y,
            OreType = z.OreType,
            Richness = z.Richness,
            DepletedPct = z.DepletedPct,
            IsSalvageZone = z.IsSalvageZone
        }).ToList(),
        Workers = state.Workers.Select(w => new WorkerEntity
        {
            Id = w.Id,
            MineId = state.Id,
            Name = w.Name,
            Skill = w.Skill,
            Salary = w.Salary,
            AssignedZoneId = w.AssignedZoneId
        }).ToList()
    };
}
