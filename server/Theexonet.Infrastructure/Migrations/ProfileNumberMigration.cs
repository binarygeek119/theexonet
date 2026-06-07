using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Infrastructure.Migrations;

public class ProfileNumberMigration(PlayerProfileUpgrader upgrader) : IDataMigration
{
    public string Id => PlayerDataMigrations.ProfileNumbers;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        upgrader.AssignAllMissingProfileNumbersAsync(cancellationToken);
}
