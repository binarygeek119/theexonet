using Theexonet.Core.Constants;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public class StarterMineGenerator : IStarterMineGenerator
{
    private static readonly string[] WorkerNames =
    [
        "Axel Voss", "Mira Chen", "Dax Okonkwo", "Lena Frost", "Rook Vega"
    ];

    public (MineState Mine, List<InventoryItemState> StarterInventory) Generate(Guid playerId, int asteroidSeed)
    {
        var rng = new Random(asteroidSeed);
        var mineId = Guid.NewGuid();
        var mine = new MineState
        {
            Id = mineId,
            PlayerId = playerId,
            Name = "Starter Claim Alpha",
            AsteroidSeed = asteroidSeed,
            Status = MineStatus.Active,
            PurchasedAt = DateTime.UtcNow
        };

        var oreTypes = new[] { OreType.Ferroxite, OreType.Voidium, OreType.Stellarite };
        for (var y = 0; y < GameBalance.GridSize; y++)
        {
            for (var x = 0; x < GameBalance.GridSize; x++)
            {
                var isSalvage = x == 0 && y == 0;
                mine.Zones.Add(new MineZoneState
                {
                    Id = Guid.NewGuid(),
                    MineId = mineId,
                    X = x,
                    Y = y,
                    OreType = isSalvage ? OreType.SalvageScrap : oreTypes[rng.Next(oreTypes.Length)],
                    Richness = isSalvage ? 0.35m : (decimal)(0.5 + rng.NextDouble() * 0.5),
                    DepletedPct = 0m,
                    IsSalvageZone = isSalvage
                });
            }
        }

        for (var i = 0; i < GameBalance.StarterWorkerCount; i++)
        {
            mine.Workers.Add(new WorkerState
            {
                Id = Guid.NewGuid(),
                MineId = mineId,
                Name = WorkerNames[i],
                Skill = 1 + rng.Next(3),
                Salary = 80m + i * 15m,
                AssignedZoneId = null
            });
        }

        var inventory = new List<InventoryItemState>();
        foreach (var supplyType in Enum.GetValues<SupplyType>())
        {
            inventory.Add(new InventoryItemState
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Category = ItemCategory.Supply,
                ItemType = supplyType.ToString(),
                Quantity = GameBalance.StarterSupplyQuantity,
                Condition = GameBalance.MaxCondition,
                IsNew = false,
            });
        }

        return (mine, inventory);
    }
}
