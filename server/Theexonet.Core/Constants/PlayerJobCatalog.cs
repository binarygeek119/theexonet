namespace Theexonet.Core.Constants;

public sealed record PlayerJobDefinition(string Slug, string Title, string Description);

public static class PlayerJobCatalog
{
    public const string AsteroidMiner = "asteroid_miner";

    public static readonly IReadOnlyList<PlayerJobDefinition> All =
    [
        new(AsteroidMiner, "Asteroid Miner", "Extract ore from claim asteroids across the belt."),
    ];

    public static PlayerJobDefinition? TryGet(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        return All.FirstOrDefault(job =>
            string.Equals(job.Slug, normalized, StringComparison.Ordinal));
    }
}
