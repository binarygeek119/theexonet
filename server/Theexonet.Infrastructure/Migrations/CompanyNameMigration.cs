using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Infrastructure.Migrations;

public class CompanyNameMigration(CompanyNameService companyNameService) : IDataMigration
{
    public string Id => PlayerDataMigrations.CompanyNames;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        companyNameService.AssignUniqueNamesToMinesMissingThemAsync(cancellationToken);
}
