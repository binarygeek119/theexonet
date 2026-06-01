using Rava.Infrastructure.Data;
using Rava.Infrastructure.Services;

namespace Rava.Infrastructure.Migrations;

/// <summary>
/// Add a new migration class implementing <see cref="IDataMigration"/> and register it in Program.cs.
/// Use a date-prefixed id (for example 20250601-my-new-field) so migrations run in order once.
/// </summary>
public static class PlayerDataMigrations
{
    public const string ProfileDefaults = "20250529-profile-defaults";
    public const string ProfileNumbers = "20250529-profile-numbers";
    public const string CompanyNames = "20250529-company-names";
}
