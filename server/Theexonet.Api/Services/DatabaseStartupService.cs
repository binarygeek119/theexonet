using Microsoft.EntityFrameworkCore;
using Theexonet.Infrastructure;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Migrations;

namespace Theexonet.Api.Services;

/// <summary>
/// Applies schema updates after Kestrel starts so /api/status can respond while migrations run.
/// </summary>
public sealed class DatabaseStartupService(
    IServiceScopeFactory scopeFactory,
    StartupReadiness readiness,
    ILogger<DatabaseStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Database startup: applying schema and pending data migrations.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var hasBaseSchema = await HasPlayersTableAsync(db, cancellationToken);
            if (hasBaseSchema)
            {
                await DatabaseSchemaUpdater.ApplyAsync(db, cancellationToken);
                db.Database.EnsureCreated();
            }
            else
            {
                db.Database.EnsureCreated();
                await DatabaseSchemaUpdater.ApplyAsync(db, cancellationToken);
            }

            await scope.ServiceProvider.GetRequiredService<PlayerDataMigrationRunner>()
                .RunPendingAsync(cancellationToken);
            readiness.MarkDatabaseReady();
            logger.LogInformation("Database startup completed.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Database startup failed. Verify ConnectionStrings:DefaultConnection in appsettings.json.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> HasPlayersTableAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        return await db.Database.SqlQueryRaw<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'Players') AS "Value"
                """)
            .SingleAsync(cancellationToken);
    }
}
