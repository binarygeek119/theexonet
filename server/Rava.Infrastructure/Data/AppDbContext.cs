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
    public DbSet<CompanyNameLimboEntity> CompanyNameLimbo => Set<CompanyNameLimboEntity>();
    public DbSet<CompanyNameListingEntity> CompanyNameListings => Set<CompanyNameListingEntity>();
    public DbSet<AccountResetEntity> AccountResets => Set<AccountResetEntity>();
    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();
    public DbSet<ProfileFlagEntity> ProfileFlags => Set<ProfileFlagEntity>();
    public DbSet<PlayerBanEntity> PlayerBans => Set<PlayerBanEntity>();
    public DbSet<BanAppealEntity> BanAppeals => Set<BanAppealEntity>();
    public DbSet<StaffMessageEntity> StaffMessages => Set<StaffMessageEntity>();
    public DbSet<PlayerMessageEntity> PlayerMessages => Set<PlayerMessageEntity>();
    public DbSet<PeerMessageEntity> PeerMessages => Set<PeerMessageEntity>();
    public DbSet<PlayerToStaffMessageEntity> PlayerToStaffMessages => Set<PlayerToStaffMessageEntity>();
    public DbSet<FlaggedMessageEntity> FlaggedMessages => Set<FlaggedMessageEntity>();
    public DbSet<PlayerWarningEntity> PlayerWarnings => Set<PlayerWarningEntity>();
    public DbSet<SpecialEventEntity> SpecialEvents => Set<SpecialEventEntity>();
    public DbSet<SpecialEventRewardEntity> SpecialEventRewards => Set<SpecialEventRewardEntity>();
    public DbSet<SpecialEventClaimEntity> SpecialEventClaims => Set<SpecialEventClaimEntity>();
    public DbSet<SpecialEventProgressEntity> SpecialEventProgress => Set<SpecialEventProgressEntity>();
    public DbSet<SpecialEventAnnouncementEntity> SpecialEventAnnouncements => Set<SpecialEventAnnouncementEntity>();
    public DbSet<DataMigrationEntity> DataMigrations => Set<DataMigrationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(e =>
        {
            e.HasIndex(p => p.Username).IsUnique();
            e.HasIndex(p => p.Email).IsUnique();
            e.HasIndex(p => p.ProfileNumber).IsUnique();
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

        modelBuilder.Entity<PasswordResetTokenEntity>(e =>
        {
            e.HasIndex(t => t.TokenHash);
            e.HasOne(t => t.Player)
                .WithMany()
                .HasForeignKey(t => t.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FriendshipEntity>(e =>
        {
            e.HasOne(f => f.Player)
                .WithMany()
                .HasForeignKey(f => f.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Friend)
                .WithMany()
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileFlagEntity>(e =>
        {
            e.HasIndex(f => f.PlayerId);
            e.HasOne(f => f.Player)
                .WithMany()
                .HasForeignKey(f => f.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerBanEntity>(e =>
        {
            e.HasIndex(b => b.PlayerId);
            e.HasOne(b => b.Player)
                .WithMany()
                .HasForeignKey(b => b.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BanAppealEntity>(e =>
        {
            e.HasIndex(a => a.PlayerId);
            e.HasIndex(a => a.Status);
            e.HasOne(a => a.Player)
                .WithMany()
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffMessageEntity>(e =>
        {
            e.HasIndex(m => m.ToUsername);
            e.HasIndex(m => m.FromUsername);
            e.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<PlayerMessageEntity>(e =>
        {
            e.HasIndex(m => m.PlayerId);
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.Player)
                .WithMany()
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PeerMessageEntity>(e =>
        {
            e.HasIndex(m => m.FromPlayerId);
            e.HasIndex(m => m.ToPlayerId);
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.FromPlayer)
                .WithMany()
                .HasForeignKey(m => m.FromPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.ToPlayer)
                .WithMany()
                .HasForeignKey(m => m.ToPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerToStaffMessageEntity>(e =>
        {
            e.HasIndex(m => m.PlayerId);
            e.HasIndex(m => m.ToStaffUsername);
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.Player)
                .WithMany()
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FlaggedMessageEntity>(e =>
        {
            e.HasIndex(m => m.PlayerId);
            e.HasIndex(m => m.Status);
            e.HasIndex(m => m.CreatedAt);
            e.HasOne(m => m.Player)
                .WithMany()
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlayerWarningEntity>(e =>
        {
            e.HasIndex(w => w.PlayerId);
            e.HasIndex(w => w.CreatedAt);
            e.HasIndex(w => w.ExpiresAt);
            e.HasOne(w => w.Player)
                .WithMany()
                .HasForeignKey(w => w.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.FlaggedMessage)
                .WithMany()
                .HasForeignKey(w => w.FlaggedMessageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SpecialEventEntity>(e =>
        {
            e.HasIndex(ev => ev.IsActive);
            e.HasMany(ev => ev.Rewards)
                .WithOne(r => r.Event)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(ev => ev.Claims)
                .WithOne(c => c.Event)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpecialEventClaimEntity>(e =>
        {
            e.HasIndex(c => c.EventId);
            e.HasIndex(c => new { c.PlayerId, c.EventId }).IsUnique();
            e.HasOne(c => c.Player)
                .WithMany()
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpecialEventProgressEntity>(e =>
        {
            e.HasIndex(p => p.EventId);
            e.HasIndex(p => new { p.PlayerId, p.EventId }).IsUnique();
            e.HasOne(p => p.Player)
                .WithMany()
                .HasForeignKey(p => p.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Event)
                .WithMany(ev => ev.ProgressEntries)
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpecialEventAnnouncementEntity>(e =>
        {
            e.HasIndex(a => a.EventId);
            e.HasIndex(a => new { a.PlayerId, a.EventId }).IsUnique();
            e.HasOne(a => a.Player)
                .WithMany()
                .HasForeignKey(a => a.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Event)
                .WithMany(ev => ev.Announcements)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompanyNameLimboEntity>(e =>
        {
            e.HasIndex(l => l.NormalizedName);
            e.HasIndex(l => l.AvailableAfter);
            e.HasIndex(l => l.PlayerId);
            e.HasOne(l => l.Player)
                .WithMany()
                .HasForeignKey(l => l.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CompanyNameListingEntity>(e =>
        {
            e.HasIndex(l => l.NormalizedName);
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.SellerPlayerId);
            e.HasOne(l => l.Seller)
                .WithMany()
                .HasForeignKey(l => l.SellerPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
