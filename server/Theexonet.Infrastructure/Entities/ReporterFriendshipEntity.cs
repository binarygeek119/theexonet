namespace Theexonet.Infrastructure.Entities;

public class ReporterFriendshipEntity
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string ReporterSlug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlayerEntity Player { get; set; } = null!;
}
