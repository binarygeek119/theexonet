using Rava.Infrastructure.Data;
using Rava.Infrastructure.Services;

namespace Rava.Infrastructure.Migrations;

public class ProfileNumberMigration(PlayerProfileUpgrader upgrader) : IDataMigration
{
    public string Id => PlayerDataMigrations.ProfileNumbers;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        upgrader.AssignAllMissingProfileNumbersAsync(cancellationToken);
}
