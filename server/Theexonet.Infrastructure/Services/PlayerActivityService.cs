using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Theexonet.Infrastructure.Data;

namespace Theexonet.Infrastructure.Services;

public class PlayerActivityService(AppDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(2);

    public async Task TouchAsync(Guid playerId, CancellationToken ct)
    {
        if (playerId == Guid.Empty)
        {
            return;
        }

        var cacheKey = $"player-activity:{playerId:D}";
        if (cache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        cache.Set(cacheKey, true, TouchInterval);

        await db.Players
            .Where(player => player.Id == playerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(player => player.LastSeenAtUtc, DateTime.UtcNow),
                ct);
    }
}
