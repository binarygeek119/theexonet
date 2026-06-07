using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Infrastructure.Migrations;

public class ProfileDefaultsMigration(PlayerProfileUpgrader upgrader) : IDataMigration
{
    public string Id => PlayerDataMigrations.ProfileDefaults;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        upgrader.UpgradeAllProfileDefaultsAsync(cancellationToken);
}
