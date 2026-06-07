namespace Theexonet.Infrastructure.Entities;

public class DataMigrationEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
