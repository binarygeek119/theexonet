using Microsoft.EntityFrameworkCore;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Core.Validation;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class PublicProfileService(
    AppDbContext db,
    IMarketItemsCatalog marketItems,
    PlayerBanService playerBanService,
    RavaHostingPaths hostingPaths)
{
    public const string SortCompanyValue = "companyValue";

    public static readonly IReadOnlyList<string> ComingSoonLeaderboardSorts =
    [
        "gameDay",
        "workers",
        "zones",
        "oreStockpile",
        "runwayDays",
    ];

    public async Task<PublicProfileSearchResponse> SearchAsync(
        string query,
        string mode,
        int limit,
        CancellationToken ct)
    {
        query = query.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PublicProfileSearchResponse(query, NormalizeMode(mode), []);
        }

        limit = Math.Clamp(limit, 1, 50);
        var normalizedMode = NormalizeMode(mode);
        var players = normalizedMode switch
        {
            "profileNumber" => await SearchByProfileNumberAsync(query, limit, ct),
            "companyName" => await SearchByCompanyNameAsync(query, limit, ct),
            "username" => await SearchByUsernameAsync(query, limit, ct),
            _ => await SearchAutoAsync(query, limit, ct),
        };

        var summaries = await MapSummariesAsync(players, ct);
        var merged = MergeReporterSearchResults(query, limit, summaries, hostingPaths.ReporterAssetRoots());
        return new PublicProfileSearchResponse(query, normalizedMode, merged);
    }

    public async Task<PublicProfileLeaderboardResponse> GetLeaderboardAsync(
        string sort,
        int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        var normalizedSort = NormalizeSort(sort);
        if (normalizedSort != SortCompanyValue)
        {
            return new PublicProfileLeaderboardResponse(normalizedSort, [], ComingSoonLeaderboardSorts);
        }

        var players = await db.Players.AsNoTracking()
            .Include(p => p.Inventory)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Workers)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Zones)
            .ToListAsync(ct);

        var visiblePlayers = await FilterPubliclyVisibleAsync(players, ct);
        var ranked = visiblePlayers
            .Select(player =>
            {
                var mine = GetActiveMine(player);
                return new
                {
                    Player = player,
                    Mine = mine,
                    CompanyValue = ComputeCompanyValue(player),
                };
            })
            .OrderByDescending(entry => entry.CompanyValue)
            .ThenBy(entry => entry.Player.Username, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        var entries = new List<PublicProfileSummaryDto>();
        for (var index = 0; index < ranked.Count; index++)
        {
            var entry = ranked[index];
            entries.Add(MapSummary(entry.Player, entry.Mine, entry.CompanyValue, index + 1));
        }

        return new PublicProfileLeaderboardResponse(normalizedSort, entries, ComingSoonLeaderboardSorts);
    }

    public async Task<PublicProfileDetailDto?> GetByUsernameAsync(string username, CancellationToken ct)
    {
        var reporter = OffworldNewsReporterSocial.TryGetByUsername(username);
        if (reporter is not null)
        {
            return OffworldNewsReporterProfileMapper.ToPublicDetail(reporter, hostingPaths);
        }

        var normalized = username.Trim().ToLowerInvariant();
        var player = await db.Players.AsNoTracking()
            .Include(p => p.Inventory)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Workers)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Zones)
            .FirstOrDefaultAsync(p => p.Username.ToLower() == normalized, ct);

        if (player is null || !await IsPubliclyVisibleAsync(player.Id, ct))
        {
            return null;
        }

        return MapDetail(player, GetActiveMine(player), ComputeCompanyValue(player));
    }

    public async Task<OffworldNewsCompanyContext> GetNewsCompanyContextAsync(CancellationToken ct)
    {
        const int poolSize = 5;
        var players = await db.Players.AsNoTracking()
            .Include(p => p.Inventory)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Workers)
            .Include(p => p.Mines.Where(m => m.Status == MineStatus.Active))
                .ThenInclude(m => m.Zones)
            .ToListAsync(ct);

        var visiblePlayers = await FilterPubliclyVisibleAsync(players, ct);
        var ranked = visiblePlayers
            .Select(player =>
            {
                var mine = GetActiveMine(player);
                return new
                {
                    CompanyName = mine?.Name,
                    CompanyValue = ComputeCompanyValue(player),
                };
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CompanyName))
            .OrderByDescending(entry => entry.CompanyValue)
            .ThenBy(entry => entry.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rising = ranked
            .Take(poolSize)
            .Select(entry => entry.CompanyName!)
            .ToList();

        var struggling = ranked
            .TakeLast(Math.Min(poolSize, ranked.Count))
            .Select(entry => entry.CompanyName!)
            .Where(name => !rising.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new OffworldNewsCompanyContext(rising, struggling);
    }

    private static IReadOnlyList<PublicProfileSummaryDto> MergeReporterSearchResults(
        string query,
        int limit,
        IReadOnlyList<PublicProfileSummaryDto> playerSummaries,
        string[] reporterAssetRoots)
    {
        var reporters = new List<PublicProfileSummaryDto>();
        var byNumber = OffworldNewsReporterSocial.TryGetByProfileNumber(
            ProfileNumberNormalizer.Normalize(query));
        if (byNumber is not null)
        {
            reporters.Add(OffworldNewsReporterProfileMapper.ToPublicSummary(byNumber, 0, reporterAssetRoots));
        }

        foreach (var reporter in OffworldNewsReporterSocial.Search(query, limit))
        {
            if (reporters.Any(r => string.Equals(r.ReporterSlug, reporter.Slug, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            reporters.Add(OffworldNewsReporterProfileMapper.ToPublicSummary(reporter, 0, reporterAssetRoots));
        }

        if (reporters.Count == 0)
        {
            return playerSummaries;
        }

        var playerNumbers = new HashSet<string>(
            playerSummaries.Select(p => p.ProfileNumber),
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<PublicProfileSummaryDto>();
        foreach (var reporter in reporters)
        {
            if (!playerNumbers.Contains(reporter.ProfileNumber))
            {
                merged.Add(reporter);
            }
        }

        foreach (var player in playerSummaries)
        {
            if (merged.Count >= limit)
            {
                break;
            }

            merged.Add(player);
        }

        return merged.Take(limit).ToList();
    }

    private async Task<List<PlayerEntity>> SearchAutoAsync(string query, int limit, CancellationToken ct)
    {
        var normalizedNumber = ProfileNumberNormalizer.Normalize(query);
        if (!string.IsNullOrWhiteSpace(normalizedNumber))
        {
            if (OffworldNewsReporterSocial.TryGetByProfileNumber(normalizedNumber) is not null)
            {
                return [];
            }

            var byNumber = await db.Players.AsNoTracking()
                .Where(p => p.ProfileNumber == normalizedNumber)
                .Take(limit)
                .ToListAsync(ct);
            if (byNumber.Count > 0)
            {
                return await FilterPubliclyVisibleAsync(byNumber, ct);
            }
        }

        var exactUsername = await db.Players.AsNoTracking()
            .Where(p => p.Username.ToLower() == query.ToLowerInvariant())
            .Take(limit)
            .ToListAsync(ct);
        if (exactUsername.Count > 0)
        {
            return await FilterPubliclyVisibleAsync(exactUsername, ct);
        }

        var byCompany = await SearchByCompanyNameAsync(query, limit, ct);
        if (byCompany.Count > 0)
        {
            return byCompany;
        }

        return await SearchByUsernameAsync(query, limit, ct);
    }

    private async Task<List<PlayerEntity>> SearchByUsernameAsync(string query, int limit, CancellationToken ct)
    {
        var term = $"%{query.Trim()}%";
        var players = await db.Players.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Username, term))
            .OrderBy(p => p.Username)
            .Take(limit)
            .ToListAsync(ct);

        return await FilterPubliclyVisibleAsync(players, ct);
    }

    private async Task<List<PlayerEntity>> SearchByProfileNumberAsync(string query, int limit, CancellationToken ct)
    {
        var normalized = ProfileNumberNormalizer.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var players = await db.Players.AsNoTracking()
            .Where(p => p.ProfileNumber == normalized || EF.Functions.ILike(p.ProfileNumber, $"%{normalized}%"))
            .OrderBy(p => p.ProfileNumber)
            .Take(limit)
            .ToListAsync(ct);

        return await FilterPubliclyVisibleAsync(players, ct);
    }

    private async Task<List<PlayerEntity>> SearchByCompanyNameAsync(string query, int limit, CancellationToken ct)
    {
        var term = $"%{query.Trim()}%";
        var playerIds = await db.Mines.AsNoTracking()
            .Where(m => m.Status == MineStatus.Active && EF.Functions.ILike(m.Name, term))
            .Select(m => m.PlayerId)
            .Distinct()
            .Take(limit)
            .ToListAsync(ct);

        if (playerIds.Count == 0)
        {
            return [];
        }

        var players = await db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .OrderBy(p => p.Username)
            .ToListAsync(ct);

        return await FilterPubliclyVisibleAsync(players, ct);
    }

    private async Task<IReadOnlyList<PublicProfileSummaryDto>> MapSummariesAsync(
        IReadOnlyList<PlayerEntity> players,
        CancellationToken ct)
    {
        if (players.Count == 0)
        {
            return [];
        }

        var ids = players.Select(p => p.Id).ToList();
        var inventories = await db.Inventory.AsNoTracking()
            .Where(i => ids.Contains(i.PlayerId))
            .ToListAsync(ct);
        var inventoryByPlayer = inventories
            .GroupBy(i => i.PlayerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mines = await db.Mines.AsNoTracking()
            .Where(m => ids.Contains(m.PlayerId) && m.Status == MineStatus.Active)
            .Include(m => m.Workers)
            .Include(m => m.Zones)
            .ToListAsync(ct);
        var mineByPlayer = mines
            .GroupBy(m => m.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.PurchasedAt).First());

        return players
            .Select(player =>
            {
                inventoryByPlayer.TryGetValue(player.Id, out var inventory);
                mineByPlayer.TryGetValue(player.Id, out var mine);
                player.Inventory = inventory ?? [];
                return MapSummary(player, mine, ComputeCompanyValue(player, inventory));
            })
            .ToList();
    }

    private async Task<List<PlayerEntity>> FilterPubliclyVisibleAsync(
        IReadOnlyList<PlayerEntity> players,
        CancellationToken ct)
    {
        if (players.Count == 0)
        {
            return [];
        }

        var activeBans = await playerBanService.GetActiveBansForPlayersAsync(
            players.Select(p => p.Id).ToList(),
            ct);

        return players
            .Where(player => !activeBans.ContainsKey(player.Id))
            .ToList();
    }

    private async Task<bool> IsPubliclyVisibleAsync(Guid playerId, CancellationToken ct)
    {
        var ban = await playerBanService.GetActiveBanAsync(playerId, ct);
        return ban is null;
    }

    private decimal ComputeCompanyValue(PlayerEntity player, IReadOnlyList<InventoryItemEntity>? inventory = null)
    {
        var items = inventory ?? player.Inventory.ToList();
        var snapshots = items.Select(i => new InventoryItemSnapshot(i.Category, i.ItemType, i.Quantity));
        return CompanyValueCalculator.Compute(player.Credits, snapshots, marketItems);
    }

    private static MineEntity? GetActiveMine(PlayerEntity player) =>
        player.Mines
            .Where(m => m.Status == MineStatus.Active)
            .OrderBy(m => m.PurchasedAt)
            .FirstOrDefault();

    private static PublicProfileSummaryDto MapSummary(
        PlayerEntity player,
        MineEntity? mine,
        decimal companyValue,
        int rank = 0) =>
        new(
            player.Username,
            player.ProfileNumber,
            mine?.Name ?? "No active mine",
            FormatProfileImageUrl(mine?.CompanyLogoUrl ?? string.Empty, mine?.CompanyLogoRevision ?? 0),
            player.ProfileMood,
            FormatProfileImageUrl(player.ProfileImageUrl, player.ProfileImageRevision),
            player.CurrentGameDay,
            mine?.Workers.Count ?? 0,
            mine?.Zones.Count ?? 0,
            companyValue,
            rank);

    private static PublicProfileDetailDto MapDetail(
        PlayerEntity player,
        MineEntity? mine,
        decimal companyValue) =>
        new(
            player.Username,
            player.ProfileNumber,
            mine?.Name ?? "No active mine",
            FormatProfileImageUrl(mine?.CompanyLogoUrl ?? string.Empty, mine?.CompanyLogoRevision ?? 0),
            FormatProfileImageUrl(player.ProfileImageUrl, player.ProfileImageRevision),
            player.ProfileMood,
            player.ProfileAboutMe,
            player.ProfileInterests,
            player.ProfileMusic,
            player.ProfileDiscord,
            player.ProfileBluesky,
            player.ProfileTwitter,
            player.ProfileYoutube,
            player.ProfileFacebook,
            player.CreatedAt,
            player.CurrentGameDay,
            mine?.Workers.Count ?? 0,
            mine?.Zones.Count ?? 0,
            companyValue);

    private static string FormatProfileImageUrl(string url, int revision)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return revision > 0 ? $"{url}?v={revision}" : url;
    }

    private static string NormalizeMode(string mode) =>
        (mode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "username" => "username",
            "profilenumber" or "profile-number" or "profile_number" or "number" => "profileNumber",
            "companyname" or "company-name" or "company_name" or "company" => "companyName",
            _ => "auto",
        };

    private static string NormalizeSort(string sort)
    {
        var normalized = (sort ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            SortCompanyValue or "company-value" or "company_value" or "value" => SortCompanyValue,
            _ => normalized,
        };
    }
}
