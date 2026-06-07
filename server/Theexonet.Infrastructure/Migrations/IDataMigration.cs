using Theexonet.Infrastructure.Data;

namespace Theexonet.Infrastructure.Migrations;

public interface IDataMigration
{
    string Id { get; }

    Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken);
}
