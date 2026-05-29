using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Mapping;

public static class EntityMapper
{
    public static PlayerState ToState(PlayerEntity entity) => new()
    {
        Id = entity.Id,
        Username = entity.Username,
        Email = entity.Email,
        PasswordHash = entity.PasswordHash,
        Credits = entity.Credits,
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
        Quantity = entity.Quantity
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
    }

    public static InventoryItemEntity ToEntity(InventoryItemState state) => new()
    {
        Id = state.Id,
        PlayerId = state.PlayerId,
        Category = state.Category,
        ItemType = state.ItemType,
        Quantity = state.Quantity
    };

    public static MineEntity ToEntity(MineState state, Guid playerId) => new()
    {
        Id = state.Id,
        PlayerId = playerId,
        Name = state.Name,
        AsteroidSeed = state.AsteroidSeed,
        Status = state.Status,
        PurchasedAt = state.PurchasedAt,
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
