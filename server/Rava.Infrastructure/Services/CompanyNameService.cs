using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Services;
using Rava.Core.Validation;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class CompanyNameService(AppDbContext db)
{
    public async Task<string> GenerateUniqueNameAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var candidate = CompanyNameGenerator.Generate();
            if (await IsNameAvailableAsync(candidate, excludeMineId: null, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to assign a unique company name.");
    }

    public async Task AssignUniqueNameToMineAsync(MineEntity mine, CancellationToken ct)
    {
        mine.Name = await GenerateUniqueNameAsync(ct);
    }

    public async Task<(CompanyNameActionResponse? Result, string? Error)> RenameMineAsync(
        Guid playerId,
        string requestedName,
        CancellationToken ct)
    {
        var validationError = CompanyNameValidator.Validate(requestedName);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        var displayName = CompanyNameNormalizer.NormalizeDisplay(requestedName);
        var normalized = CompanyNameNormalizer.NormalizeKey(displayName);

        var mine = await LoadActiveMineAsync(playerId, ct);
        if (mine is null)
        {
            return (null, "No active mine found.");
        }

        if (CompanyNameNormalizer.NormalizeKey(mine.Name) == normalized)
        {
            return (null, "That is already your company name.");
        }

        if (await HasActiveListingAsync(playerId, ct))
        {
            return (null, "Cancel your company name listing before renaming.");
        }

        if (!await IsNameAvailableAsync(displayName, mine.Id, ct))
        {
            return (null, "That company name is already in use or reserved.");
        }

        var oldName = mine.Name;
        mine.Name = displayName;
        await PutNameInLimboAsync(oldName, ct);
        await db.SaveChangesAsync(ct);

        return (await BuildActionResponseAsync(playerId, mine, "Company name updated.", ct), null);
    }

    public async Task<(CompanyNameActionResponse? Result, string? Error)> RegenerateMineNameAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var mine = await LoadActiveMineAsync(playerId, ct);
        if (mine is null)
        {
            return (null, "No active mine found.");
        }

        if (await HasActiveListingAsync(playerId, ct))
        {
            return (null, "Cancel your company name listing before generating a new name.");
        }

        var oldName = mine.Name;
        mine.Name = await GenerateUniqueNameAsync(ct);
        await PutNameInLimboAsync(oldName, ct);
        await db.SaveChangesAsync(ct);

        return (await BuildActionResponseAsync(playerId, mine, "New company name generated.", ct), null);
    }

    public async Task<(CompanyNameActionResponse? Result, string? Error)> CreateListingAsync(
        Guid playerId,
        decimal price,
        CancellationToken ct)
    {
        if (price < CompanyNameFormats.MinListingPrice || price > CompanyNameFormats.MaxListingPrice)
        {
            return (null, $"Listing price must be between {CompanyNameFormats.MinListingPrice:0} and {CompanyNameFormats.MaxListingPrice:0} credits.");
        }

        var mine = await LoadActiveMineAsync(playerId, ct);
        if (mine is null)
        {
            return (null, "No active mine found.");
        }

        if (await HasActiveListingAsync(playerId, ct))
        {
            return (null, "You already have a company name listed for sale.");
        }

        var normalized = CompanyNameNormalizer.NormalizeKey(mine.Name);
        db.CompanyNameListings.Add(new CompanyNameListingEntity
        {
            Id = Guid.NewGuid(),
            SellerPlayerId = playerId,
            SellerMineId = mine.Id,
            CompanyName = mine.Name,
            NormalizedName = normalized,
            Price = price,
            Status = CompanyNameListingStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return (await BuildActionResponseAsync(playerId, mine, "Company name listed in the store.", ct), null);
    }

    public async Task<(CompanyNameActionResponse? Result, string? Error)> CancelListingAsync(
        Guid playerId,
        Guid listingId,
        CancellationToken ct)
    {
        var listing = await db.CompanyNameListings.FirstOrDefaultAsync(
            l => l.Id == listingId && l.SellerPlayerId == playerId,
            ct);

        if (listing is null || listing.Status != CompanyNameListingStatuses.Active)
        {
            return (null, "Listing not found.");
        }

        listing.Status = CompanyNameListingStatuses.Cancelled;
        await db.SaveChangesAsync(ct);

        var mine = await LoadActiveMineAsync(playerId, ct);
        if (mine is null)
        {
            return (null, "No active mine found.");
        }

        return (await BuildActionResponseAsync(playerId, mine, "Company name listing cancelled.", ct), null);
    }

    public async Task<(bool Success, string Message)> PurchaseListingAsync(
        Guid buyerId,
        Guid listingId,
        CancellationToken ct)
    {
        var listing = await db.CompanyNameListings
            .Include(l => l.Seller)
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing is null || listing.Status != CompanyNameListingStatuses.Active)
        {
            return (false, "Listing not found.");
        }

        if (listing.SellerPlayerId == buyerId)
        {
            return (false, "You cannot buy your own listing.");
        }

        var buyer = await db.Players.FirstOrDefaultAsync(p => p.Id == buyerId, ct);
        var buyerMine = await LoadActiveMineAsync(buyerId, ct);
        var sellerMine = await db.Mines.FirstOrDefaultAsync(
            m => m.Id == listing.SellerMineId && m.PlayerId == listing.SellerPlayerId,
            ct);

        if (buyer is null || buyerMine is null || sellerMine is null)
        {
            return (false, "Unable to complete purchase.");
        }

        if (buyer.Credits < listing.Price)
        {
            return (false, "Not enough credits.");
        }

        if (CompanyNameNormalizer.NormalizeKey(buyerMine.Name) == listing.NormalizedName)
        {
            return (false, "You already use that company name.");
        }

        var buyerOldName = buyerMine.Name;
        var purchasedName = listing.CompanyName;

        buyerMine.Name = purchasedName;
        await PutNameInLimboAsync(buyerOldName, ct);

        sellerMine.Name = await GenerateUniqueNameAsync(ct);

        buyer.Credits -= listing.Price;
        listing.Seller.Credits += listing.Price;

        var gameDay = buyer.CurrentGameDay;
        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = buyerId,
            Type = TransactionType.CompanyNamePurchase,
            Amount = -listing.Price,
            Description = $"Purchased company name \"{purchasedName}\"",
            GameDay = gameDay
        });
        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = listing.SellerPlayerId,
            Type = TransactionType.CompanyNameSale,
            Amount = listing.Price,
            Description = $"Sold company name \"{purchasedName}\"",
            GameDay = listing.Seller.CurrentGameDay
        });

        listing.Status = CompanyNameListingStatuses.Sold;
        listing.SoldAt = DateTime.UtcNow;
        listing.BuyerPlayerId = buyerId;

        await db.SaveChangesAsync(ct);
        return (true, $"You now own \"{purchasedName}\".");
    }

    public async Task<CompanyNameListingsResponse> GetActiveListingsAsync(CancellationToken ct)
    {
        await ReleaseExpiredLimboAsync(ct);

        var listings = await db.CompanyNameListings.AsNoTracking()
            .Include(l => l.Seller)
            .Where(l => l.Status == CompanyNameListingStatuses.Active)
            .OrderBy(l => l.CompanyName)
            .Select(l => new CompanyNameListingDto(
                l.Id,
                l.CompanyName,
                l.Seller.Username,
                l.Price,
                l.CreatedAt))
            .ToListAsync(ct);

        return new CompanyNameListingsResponse(listings);
    }

    public async Task<(Guid? ListingId, decimal? Price)> GetActiveListingForPlayerAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var listing = await db.CompanyNameListings.AsNoTracking()
            .Where(l => l.SellerPlayerId == playerId && l.Status == CompanyNameListingStatuses.Active)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return listing is null ? (null, null) : (listing.Id, listing.Price);
    }

    public async Task AssignUniqueNamesToMinesMissingThemAsync(CancellationToken ct)
    {
        var activeMines = await db.Mines
            .Where(m => m.Status == MineStatus.Active)
            .OrderBy(m => m.PurchasedAt)
            .ToListAsync(ct);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mine in activeMines)
        {
            var key = CompanyNameNormalizer.NormalizeKey(mine.Name);
            if (mine.Name == CompanyNameFormats.DefaultStarterName ||
                string.IsNullOrWhiteSpace(key) ||
                !seen.Add(key))
            {
                mine.Name = await GenerateUniqueNameAsync(ct);
                seen.Add(CompanyNameNormalizer.NormalizeKey(mine.Name));
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<CompanyNameActionResponse> BuildActionResponseAsync(
        Guid playerId,
        MineEntity mine,
        string message,
        CancellationToken ct)
    {
        var (listingId, listingPrice) = await GetActiveListingForPlayerAsync(playerId, ct);
        return new CompanyNameActionResponse(
            mine.Name,
            mine.Id,
            listingId is not null,
            listingId,
            listingPrice,
            message);
    }

    private async Task<MineEntity?> LoadActiveMineAsync(Guid playerId, CancellationToken ct) =>
        await db.Mines
            .Where(m => m.PlayerId == playerId && m.Status == MineStatus.Active)
            .OrderBy(m => m.PurchasedAt)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> HasActiveListingAsync(Guid playerId, CancellationToken ct) =>
        await db.CompanyNameListings.AnyAsync(
            l => l.SellerPlayerId == playerId && l.Status == CompanyNameListingStatuses.Active,
            ct);

    private async Task<bool> IsNameAvailableAsync(
        string displayName,
        Guid? excludeMineId,
        CancellationToken ct)
    {
        await ReleaseExpiredLimboAsync(ct);

        var normalized = CompanyNameNormalizer.NormalizeKey(displayName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var inUseByMine = await db.Mines.AsNoTracking()
            .AnyAsync(
                m => m.Status == MineStatus.Active &&
                     m.Id != excludeMineId &&
                     EF.Functions.ILike(m.Name, displayName),
                ct);

        if (inUseByMine)
        {
            return false;
        }

        var inLimbo = await db.CompanyNameLimbo.AsNoTracking()
            .AnyAsync(l => l.NormalizedName == normalized && l.AvailableAfter > DateTime.UtcNow, ct);

        if (inLimbo)
        {
            return false;
        }

        return !await db.CompanyNameListings.AsNoTracking()
            .AnyAsync(
                l => l.Status == CompanyNameListingStatuses.Active && l.NormalizedName == normalized,
                ct);
    }

    private async Task PutNameInLimboAsync(string displayName, CancellationToken ct)
    {
        var normalized = CompanyNameNormalizer.NormalizeKey(displayName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        await ReleaseExpiredLimboAsync(ct);

        var existing = await db.CompanyNameLimbo
            .FirstOrDefaultAsync(l => l.NormalizedName == normalized, ct);

        var availableAfter = DateTime.UtcNow.AddDays(CompanyNameFormats.LimboDays);
        if (existing is null)
        {
            db.CompanyNameLimbo.Add(new CompanyNameLimboEntity
            {
                Id = Guid.NewGuid(),
                NormalizedName = normalized,
                DisplayName = CompanyNameNormalizer.NormalizeDisplay(displayName),
                AvailableAfter = availableAfter,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.DisplayName = CompanyNameNormalizer.NormalizeDisplay(displayName);
            existing.AvailableAfter = availableAfter;
            existing.CreatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ReleaseExpiredLimboAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = await db.CompanyNameLimbo
            .Where(l => l.AvailableAfter <= now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return;
        }

        db.CompanyNameLimbo.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
    }
}
