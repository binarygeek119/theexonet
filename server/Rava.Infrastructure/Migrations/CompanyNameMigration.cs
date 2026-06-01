using Rava.Infrastructure.Data;
using Rava.Infrastructure.Services;

namespace Rava.Infrastructure.Migrations;

public class CompanyNameMigration(CompanyNameService companyNameService) : IDataMigration
{
    public string Id => PlayerDataMigrations.CompanyNames;

    public Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken) =>
        companyNameService.AssignUniqueNamesToMinesMissingThemAsync(cancellationToken);
}
