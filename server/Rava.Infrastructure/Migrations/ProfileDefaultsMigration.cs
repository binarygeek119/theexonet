using Rava.Infrastructure.Data;
using Rava.Infrastructure.Services;

namespace Rava.Infrastructure.Migrations;

public class ProfileDefaultsMigration(PlayerProfileUpgrader upgrader) : IDataMigration
{
    public string Id => PlayerDataMigrations.ProfileDefaults;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        upgrader.UpgradeAllProfileDefaultsAsync(cancellationToken);
}
