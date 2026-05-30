using Rava.Infrastructure.Data;

namespace Rava.Infrastructure.Migrations;

public interface IDataMigration
{
    string Id { get; }

    Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken);
}
