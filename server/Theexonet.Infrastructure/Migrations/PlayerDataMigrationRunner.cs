using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Theexonet.Infrastructure.Data;

namespace Theexonet.Infrastructure.Migrations;

public class PlayerDataMigrationRunner(
    AppDbContext db,
    IEnumerable<IDataMigration> migrations,
    ILogger<PlayerDataMigrationRunner> logger)
{
    public async Task RunPendingAsync(CancellationToken cancellationToken = default)
    {
        var applied = await db.DataMigrations
            .AsNoTracking()
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var appliedSet = applied.ToHashSet(StringComparer.Ordinal);

        foreach (var migration in migrations.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            if (appliedSet.Contains(migration.Id))
            {
                continue;
            }

            logger.LogInformation("Applying data migration {MigrationId}", migration.Id);
            await migration.ApplyAsync(db, cancellationToken);
            db.DataMigrations.Add(new Entities.DataMigrationEntity
            {
                Id = migration.Id,
                AppliedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Applied data migration {MigrationId}", migration.Id);
        }
    }
}
