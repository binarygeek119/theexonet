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
            db.Database.EnsureCreated();
            await DatabaseSchemaUpdater.ApplyAsync(db, cancellationToken);
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
}
