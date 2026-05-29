using Microsoft.EntityFrameworkCore;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<MineEntity> Mines => Set<MineEntity>();
    public DbSet<MineZoneEntity> MineZones => Set<MineZoneEntity>();
    public DbSet<WorkerEntity> Workers => Set<WorkerEntity>();
    public DbSet<InventoryItemEntity> Inventory => Set<InventoryItemEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<MarketPriceHistoryEntity> MarketPriceHistory => Set<MarketPriceHistoryEntity>();
    public DbSet<GameWorldEntity> GameWorld => Set<GameWorldEntity>();
    public DbSet<FriendshipEntity> Friendships => Set<FriendshipEntity>();
    public DbSet<MineGroupEntity> MineGroups => Set<MineGroupEntity>();
    public DbSet<MarketListingEntity> MarketListings => Set<MarketListingEntity>();
    public DbSet<AccountResetEntity> AccountResets => Set<AccountResetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(e =>
        {
            e.HasIndex(p => p.Username).IsUnique();
            e.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<MineEntity>()
            .HasOne(m => m.Player)
            .WithMany(p => p.Mines)
            .HasForeignKey(m => m.PlayerId);

        modelBuilder.Entity<MineZoneEntity>()
            .HasOne(z => z.Mine)
            .WithMany(m => m.Zones)
            .HasForeignKey(z => z.MineId);

        modelBuilder.Entity<WorkerEntity>()
            .HasOne(w => w.Mine)
            .WithMany(m => m.Workers)
            .HasForeignKey(w => w.MineId);

        modelBuilder.Entity<InventoryItemEntity>()
            .HasOne(i => i.Player)
            .WithMany(p => p.Inventory)
            .HasForeignKey(i => i.PlayerId);

        modelBuilder.Entity<TransactionEntity>()
            .HasOne(t => t.Player)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.PlayerId);

        modelBuilder.Entity<MarketPriceHistoryEntity>()
            .HasIndex(m => new { m.GameDay, m.SupplyType })
            .IsUnique();

        modelBuilder.Entity<GameWorldEntity>().HasData(new GameWorldEntity
        {
            Id = 1,
            CurrentDay = 1,
            LastTickAt = DateTime.UtcNow,
            MarketSeed = 42
        });
    }
}
