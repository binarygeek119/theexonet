using Microsoft.EntityFrameworkCore;
using Theexonet.Core.Services;
using Theexonet.Core.Enums;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public static class InventoryStackHelper
{
    public static InventoryItemEntity? FindStack(
        IEnumerable<InventoryItemEntity> items,
        Guid playerId,
        ItemCategory category,
        string itemType,
        bool isNew) =>
        items.FirstOrDefault(i =>
            i.PlayerId == playerId &&
            i.Category == category &&
            i.ItemType == itemType &&
            i.IsNew == isNew);

    public static Task<InventoryItemEntity?> FindStackAsync(
        AppDbContext db,
        Guid playerId,
        ItemCategory category,
        string itemType,
        bool isNew,
        CancellationToken ct) =>
        db.Inventory.FirstOrDefaultAsync(i =>
            i.PlayerId == playerId &&
            i.Category == category &&
            i.ItemType == itemType &&
            i.IsNew == isNew, ct);

    public static InventoryItemEntity AddOrMerge(
        AppDbContext db,
        Guid playerId,
        ItemCategory category,
        string itemType,
        decimal quantity,
        decimal condition,
        bool isNew)
    {
        var item = db.Inventory.Local.FirstOrDefault(i =>
            i.PlayerId == playerId &&
            i.Category == category &&
            i.ItemType == itemType &&
            i.IsNew == isNew);

        if (item is null)
        {
            item = new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Category = category,
                ItemType = itemType,
                Quantity = quantity,
                Condition = condition,
                IsNew = isNew,
            };
            db.Inventory.Add(item);
            return item;
        }

        item.Condition = ItemConditionCalculator.MergeCondition(item.Quantity, item.Condition, quantity, condition);
        item.Quantity += quantity;
        return item;
    }

    public static bool TryRemoveQuantity(
        InventoryItemEntity item,
        decimal quantity,
        out string? error)
    {
        error = null;
        if (item.Quantity < quantity)
        {
            error = "Insufficient inventory.";
            return false;
        }

        if (item.Condition <= 0)
        {
            error = "This stack has no usable units.";
            return false;
        }

        item.Quantity -= quantity;
        if (item.Quantity <= 0)
        {
            item.Quantity = 0;
        }

        return true;
    }
}
