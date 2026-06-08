using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Models;
using Theexonet.Core.Services;
using Theexonet.Core.Validation;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;
using Theexonet.Infrastructure.Mapping;

namespace Theexonet.Infrastructure.Services;

public class PlayerGameService(
    AppDbContext db,
    IMineSimulationService simulation,
    IMarketDataProvider marketProvider,
    IMarketItemsCatalog marketItems,
    ITradeItemsCatalog tradeItems,
    IStarterMineGenerator starterMineGenerator,
    IPasswordHasher passwordHasher,
    PlayerProfileUpgrader profileUpgrader,
    CompanyNameService companyNameService,
    CompanyLogoQueueService companyLogoQueueService,
    IProfileAvatarStorage profileAvatarStorage,
    IProfileBackgroundStorage profileBackgroundStorage,
    ICompanyLogoStorage companyLogoStorage,
    SpecialEventService specialEventService,
    IGameCreditsConfig gameCreditsConfig,
    ReporterFriendshipService reporterFriendshipService,
    TheexonetHostingPaths hostingPaths,
    IOptionsMonitor<AdminOptions> adminOptions,
    IOptionsMonitor<ModeratorOptions> moderatorOptions,
    ILiveUpdateBroadcaster liveUpdateBroadcaster,
    OreShipmentService oreShipmentService)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DayProcessLocks = new();
    private IGameCreditsConfig Credits => gameCreditsConfig;
    private AdminOptions AdminOptions => adminOptions.CurrentValue;
    private ModeratorOptions ModeratorOptions => moderatorOptions.CurrentValue;

    public const string PasswordResetSentMessage =
        "If an account exists for that email, a password reset link has been sent.";

    public async Task<(PlayerEntity? Player, MineEntity? Mine, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var validationError = AuthValidator.ValidateRegistration(
            request.Username,
            request.Email,
            request.Password,
            request.Birthday,
            request.ProfileGender,
            request.ProfilePreferredPronouns,
            request.ProfileLocale);
        if (validationError is not null)
        {
            return (null, null, validationError);
        }

        var normalizedEmail = AuthValidator.NormalizeEmail(request.Email);
        if (await db.Players.AnyAsync(p => p.Username == request.Username || p.Email == normalizedEmail, ct))
        {
            return (null, null, "Username or email already exists.");
        }

        var birthday = DateOnly.ParseExact(request.Birthday.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var playerId = Guid.NewGuid();
        var asteroidSeed = Random.Shared.Next(1000, 999999);
        var (mineState, starterInventory) = starterMineGenerator.Generate(playerId, asteroidSeed);

        var profileGender = ProfileGender.Normalize(request.ProfileGender);
        var profilePreferredPronouns = ProfileGender.RequiresPreferredPronouns(profileGender)
            ? ProfilePreferredPronouns.Normalize(request.ProfilePreferredPronouns)
            : string.Empty;
        var profileLocale = ProfileLocale.Normalize(request.ProfileLocale);

        var player = new PlayerEntity
        {
            Id = playerId,
            Username = request.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            Credits = GameBalance.OperatingStarterFloat,
            ReserveBalance = Credits.SignUp,
            CurrentGameDay = 1,
            LastProcessedUtcDate = UtcGameClock.Today,
            Birthday = birthday,
            ProfileBirthdayPublic = request.ProfileBirthdayPublic,
            ProfileAgePublic = request.ProfileAgePublic,
            ProfileGender = profileGender,
            ProfilePreferredPronouns = profilePreferredPronouns,
            ProfileLocale = profileLocale,
            ProfileAvatarPreset = ProfileAvatarPresets.FromGender(profileGender),
            ProfileNumber = await profileUpgrader.CreateUniqueProfileNumberAsync(ct)
        };

        var mine = EntityMapper.ToEntity(mineState, playerId);
        mine.MiningRightsPaidThroughDay = player.CurrentGameDay + GameBalance.MiningRightsPeriodDays;
        await companyNameService.AssignUniqueNameToMineAsync(mine, ct);
        var inventoryEntities = starterInventory.Select(EntityMapper.ToEntity).ToList();

        player.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = ReserveTransactionType.StarterGrant,
            Amount = Credits.SignUp,
            Description = "Cosmic Reserve starter grant",
            GameDay = 1
        });

        db.Players.Add(player);
        db.Mines.Add(mine);
        db.Inventory.AddRange(inventoryEntities);
        await db.SaveChangesAsync(ct);

        return (player, mine, null);
    }

    public async Task<(PlayerEntity? Player, string? Error)> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Username == request.Username, ct);
        if (player is null || !passwordHasher.Verify(request.Password, player.PasswordHash))
        {
            return (null, "Invalid username or password.");
        }

        return (player, null);
    }

    public async Task<(string? Token, PlayerEntity? Player, string? Error)> CreatePasswordResetTokenAsync(
        string email, CancellationToken ct)
    {
        var emailError = AuthValidator.ValidateEmail(email);
        if (emailError is not null)
        {
            return (null, null, emailError);
        }

        var normalizedEmail = AuthValidator.NormalizeEmail(email);
        var player = await db.Players.FirstOrDefaultAsync(p => p.Email == normalizedEmail, ct);
        if (player is null)
        {
            return (null, null, null);
        }

        var existingTokens = await db.PasswordResetTokens
            .Where(t => t.PlayerId == player.Id && !t.Used)
            .ToListAsync(ct);
        foreach (var existing in existingTokens)
        {
            existing.Used = true;
        }

        var token = GenerateResetToken();
        db.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            TokenHash = HashResetToken(token),
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return (token, player, null);
    }

    public async Task<string?> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return "Reset token is required.";
        }

        var passwordError = AuthValidator.ValidatePassword(request.NewPassword);
        if (passwordError is not null)
        {
            return passwordError;
        }

        var tokenHash = HashResetToken(request.Token.Trim());
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.Player)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.Used, ct);

        if (resetToken is null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return "Invalid or expired reset link. Request a new password reset.";
        }

        resetToken.Player.PasswordHash = passwordHasher.Hash(request.NewPassword);
        resetToken.Used = true;

        var otherTokens = await db.PasswordResetTokens
            .Where(t => t.PlayerId == resetToken.PlayerId && !t.Used && t.Id != resetToken.Id)
            .ToListAsync(ct);
        foreach (var other in otherTokens)
        {
            other.Used = true;
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    private static string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashResetToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    public async Task<MineDetailResponse?> GetMineAsync(Guid playerId, Guid mineId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return null;
        }

        var (latestReport, eventCompletions) = await ProcessDueDaysAsync(player, ct);
        var birthdayMessage = await TryGrantBirthdayBonusAsync(player, ct);
        if (birthdayMessage is not null)
        {
            await db.Entry(player).ReloadAsync(ct);
        }

        mine = await LoadMineAsync(mineId, ct);
        if (mine is null)
        {
            return null;
        }

        var inventoryTracked = await db.Inventory
            .Where(i => i.PlayerId == playerId).ToListAsync(ct);
        await MigrateOreInventoryToStockpileAsync(playerId, mine.Id, inventoryTracked, ct);

        var inventory = await db.Inventory.AsNoTracking()
            .Where(i => i.PlayerId == playerId).ToListAsync(ct);

        var currentJob = await db.PlayerJobHistory.AsNoTracking()
            .Where(j => j.PlayerId == playerId && j.IsCurrent)
            .Select(j => new { j.JobSlug, j.JobTitle })
            .FirstOrDefaultAsync(ct);

        return MapMineDetail(
            player,
            mine,
            inventory,
            latestReport,
            birthdayMessage,
            eventCompletions,
            currentJob?.JobSlug,
            currentJob?.JobTitle);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<EventCompletionDto> EventCompletions)> AssignWorkerAsync(
        Guid playerId, Guid mineId, AssignWorkerRequest request, CancellationToken ct)
    {
        var mine = await LoadMineAsync(mineId, ct);
        if (mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.", []);
        }

        var worker = mine.Workers.FirstOrDefault(w => w.Id == request.WorkerId);
        if (worker is null)
        {
            return (false, "Worker not found.", []);
        }

        if (!string.IsNullOrEmpty(request.ZoneId))
        {
            if (!Guid.TryParse(request.ZoneId, out var zoneGuid))
            {
                return (false, "Invalid zone id.", []);
            }

            var zone = mine.Zones.FirstOrDefault(z => z.Id == zoneGuid);
            if (zone is null)
            {
                return (false, "Zone not found.", []);
            }

            if (zone.DepletedPct >= 100m && !zone.IsSalvageZone)
            {
                return (false, "Zone is fully depleted.", []);
            }

            worker.AssignedZoneId = zoneGuid;
        }
        else
        {
            worker.AssignedZoneId = null;
        }

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(request.ZoneId))
        {
            var completions = await specialEventService.RecordProgressAsync(
                playerId,
                SpecialEventChallengeType.AssignWorker,
                null,
                1,
                ct);
            LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);
            return (true, "Worker assigned to zone.", completions);
        }

        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);
        return (true, "Worker unassigned.", []);
    }

    public async Task<(bool Success, string Message, decimal? NewCredits, IReadOnlyList<EventCompletionDto> EventCompletions)> BuySupplyAsync(
        Guid playerId, Guid mineId, BuySupplyRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.", null, []);
        }

        var (_, dayCompletions) = await ProcessDueDaysAsync(player, ct);

        if (request.Quantity <= 0)
        {
            return (false, "Quantity must be positive.", null, []);
        }

        var supplyType = (SupplyType)request.SupplyType;
        if (!tradeItems.IsTradeableSupply(supplyType))
        {
            return (false, "That supply is not available in the trade market.", null, []);
        }

        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, UtcGameClock.Today, ct);
        var unitPrice = market.Prices.First(p => p.SupplyType == supplyType).Price;
        var totalCost = Math.Round(unitPrice * request.Quantity, 2);

        if (player.Credits < totalCost)
        {
            return (false, "Insufficient credits.", player.Credits, []);
        }

        InventoryStackHelper.AddOrMerge(
            db,
            playerId,
            ItemCategory.Supply,
            supplyType.ToString(),
            request.Quantity,
            GameBalance.MaxCondition,
            isNew: true);

        var eventBonuses = await specialEventService.GetActiveMarketBonusesAsync(ct);
        var tradeBonusCredits = eventBonuses.TradeBonusPercent > 0
            ? Math.Round(totalCost * eventBonuses.TradeBonusPercent / 100m, 2)
            : 0m;

        player.Credits -= totalCost;
        if (tradeBonusCredits > 0)
        {
            player.Credits += tradeBonusCredits;
            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.SpecialEventBonus,
                Amount = tradeBonusCredits,
                Description = $"Event trade bonus (+{eventBonuses.TradeBonusPercent:0.##}%)",
                GameDay = player.CurrentGameDay
            });
        }

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = TransactionType.SupplyPurchase,
            Amount = -totalCost,
            Description = $"Purchased {request.Quantity} {supplyType}",
            GameDay = player.CurrentGameDay
        });

        await db.SaveChangesAsync(ct);
        var actionCompletions = await specialEventService.RecordProgressAsync(
            playerId,
            SpecialEventChallengeType.BuySupply,
            supplyType.ToString(),
            1,
            ct);
        var completions = dayCompletions.Concat(actionCompletions).ToList();
        var message = tradeBonusCredits > 0
            ? $"Purchased {request.Quantity} {supplyType} for {totalCost} credits (+{tradeBonusCredits} event trade bonus)."
            : $"Purchased {request.Quantity} {supplyType} for {totalCost} credits.";
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);
        return (true, message, player.Credits, completions);
    }

    public async Task<(bool Success, string Message, decimal? NewCredits, IReadOnlyList<EventCompletionDto> EventCompletions)> SellOreAsync(
        Guid playerId, Guid mineId, SellOreRequest request, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        var mine = await LoadMineAsync(mineId, ct);
        if (player is null || mine is null || mine.PlayerId != playerId)
        {
            return (false, "Mine not found.", null, []);
        }

        var (_, dayCompletions) = await ProcessDueDaysAsync(player, ct);

        if (request.Quantity <= 0)
        {
            return (false, "Quantity must be positive.", null, []);
        }

        var oreType = (OreType)request.OreType;
        if (!tradeItems.IsTradeableOre(oreType))
        {
            return (false, "That ore cannot be sold in the trade market.", null, []);
        }

        var oreStacks = await db.Inventory
            .Where(i => i.PlayerId == playerId
                && i.Category == ItemCategory.Ore
                && i.ItemType == oreType.ToString()
                && i.Condition > 0
                && i.Quantity > 0)
            .OrderBy(i => i.IsNew)
            .ToListAsync(ct);

        var available = oreStacks.Sum(i => i.Quantity);
        if (available < request.Quantity)
        {
            return (false, "Insufficient ore in inventory.", player.Credits, []);
        }

        var remaining = request.Quantity;
        var conditionWeightedValue = 0m;
        foreach (var stack in oreStacks)
        {
            if (remaining <= 0)
            {
                break;
            }

            var sold = Math.Min(stack.Quantity, remaining);
            conditionWeightedValue += sold * ItemConditionCalculator.ConditionPriceFactor(stack.Condition);
            stack.Quantity -= sold;
            remaining -= sold;
        }

        var basePrice = marketItems.GetOreBasePrice(oreType);
        var rate = request.EmergencyBuyback ? GameBalance.EmergencyBuybackRate : 1m;
        var totalValue = Math.Round(basePrice * conditionWeightedValue * rate, 2);
        var eventBonuses = await specialEventService.GetActiveMarketBonusesAsync(ct);
        var saleBonusCredits = !request.EmergencyBuyback && eventBonuses.SaleBonusPercent > 0
            ? Math.Round(totalValue * eventBonuses.SaleBonusPercent / 100m, 2)
            : 0m;

        player.Credits += totalValue + saleBonusCredits;

        db.Transactions.Add(new TransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = request.EmergencyBuyback ? TransactionType.EmergencyBuyback : TransactionType.OreSale,
            Amount = totalValue,
            Description = request.EmergencyBuyback
                ? $"Emergency buy back: {request.Quantity} {oreType} at 50%"
                : $"Sold {request.Quantity} {oreType}",
            GameDay = player.CurrentGameDay
        });

        if (saleBonusCredits > 0)
        {
            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.SpecialEventBonus,
                Amount = saleBonusCredits,
                Description = $"Event sale bonus (+{eventBonuses.SaleBonusPercent:0.##}%)",
                GameDay = player.CurrentGameDay
            });
        }

        await db.SaveChangesAsync(ct);
        var actionCompletions = await specialEventService.RecordProgressAsync(
            playerId,
            SpecialEventChallengeType.SellOre,
            oreType.ToString(),
            1,
            ct);
        var completions = dayCompletions.Concat(actionCompletions).ToList();
        var message = saleBonusCredits > 0
            ? $"Sold {request.Quantity} {oreType} for {totalValue} credits (+{saleBonusCredits} event sale bonus)."
            : $"Sold {request.Quantity} {oreType} for {totalValue} credits.";
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Mine);
        return (true, message, player.Credits, completions);
    }

    public async Task<(DayAdvanceResponse? Report, IReadOnlyList<EventCompletionDto> EventCompletions)> AdvanceDayAsync(
        Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, []);
        }

        return await ProcessDueDaysAsync(player, ct);
    }

    public async Task<MarketTodayResponse?> GetMarketTodayAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        await ProcessDueDaysAsync(player, ct);
        player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId, ct);

        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, UtcGameClock.Today, ct);
        var eventBonuses = await specialEventService.GetActiveMarketBonusesAsync(ct);
        return MapMarket(market, eventBonuses);
    }

    public async Task<FinanceResponse?> GetFinancesAsync(Guid playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        await ProcessDueDaysAsync(player, ct);
        player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId, ct);

        var mine = await LoadPlayerMineAsync(playerId, ct, activeOnly: true);
        if (mine is null)
        {
            return null;
        }

        var inventory = await db.Inventory.AsNoTracking()
            .Where(i => i.PlayerId == playerId).ToListAsync(ct);
        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => t.PlayerId == playerId).ToListAsync(ct);

        var market = await GetOrCreateMarketSnapshotAsync(player.CurrentGameDay, UtcGameClock.Today, ct);
        var currentJobSlug = await db.PlayerJobHistory.AsNoTracking()
            .Where(j => j.PlayerId == playerId && j.IsCurrent)
            .Select(j => j.JobSlug)
            .FirstOrDefaultAsync(ct);
        var dailyJobSalary = CosmicReserveService.ResolveDailyJobSalary(currentJobSlug);
        var mineState = EntityMapper.ToState(mine);
        var inventoryStates = inventory.Select(EntityMapper.ToState).ToList();
        var estimatedIncome = simulation.CalculateEstimatedDailyIncome(mineState, inventoryStates);
        var obligations = CompanyObligationsCalculator.ComputePreview(
            mine.Workers.Count,
            estimatedIncome,
            player.CurrentGameDay,
            mine.MiningRightsPaidThroughDay);
        var summary = simulation.BuildFinanceSummary(
            EntityMapper.ToState(player),
            mineState,
            inventoryStates,
            transactions.Select(EntityMapper.ToState).ToList(),
            market,
            player.ReserveBalance,
            dailyJobSalary,
            obligations.Total);

        var reserveActivity = await db.ReserveTransactions.AsNoTracking()
            .Where(t => t.PlayerId == playerId && CompanyFinanceCatalog.CompanyActivityTypes.Contains(t.Type))
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        return new FinanceResponse(
            summary.Credits,
            summary.ReserveBalance,
            summary.DailyJobSalary,
            summary.DailyPayroll,
            summary.DailySupplyCost,
            summary.EstimatedDailyIncome,
            summary.DailyCompanyObligations,
            summary.DailyTotalReserveBurn,
            summary.RunwayDays,
            summary.IsSoftlocked,
            summary.CanEmergencyBuyback,
            new CompanyObligationsDto(
                obligations.CompanyTax,
                obligations.HealthInsurance,
                obligations.JobInsurance,
                obligations.BeltFee,
                obligations.MiningRights,
                obligations.Total),
            new MiningRightsDto(
                mine.MiningRightsPaidThroughDay,
                player.CurrentGameDay,
                player.CurrentGameDay > mine.MiningRightsPaidThroughDay,
                GameBalance.MiningRightsRenewalFee),
            mine.Workers
                .OrderBy(w => w.Name)
                .Select(w => new CompanyWorkerDto(w.Id, w.Name, w.Skill, w.Salary, w.AssignedZoneId))
                .ToList(),
            summary.RecentTransactions.Select(t => new TransactionDto(
                t.Type.ToString(), t.Amount, t.Description, t.GameDay, t.CreatedAt)).ToList(),
            reserveActivity.Select(t => new ReserveTransactionDto(
                t.Type.ToString(), t.Amount, t.Description, t.GameDay, t.CreatedAt)).ToList());
    }

    public async Task<Guid?> GetPrimaryMineIdAsync(Guid playerId, CancellationToken ct)
    {
        var mineId = await db.Mines
            .Where(m => m.PlayerId == playerId && m.Status == MineStatus.Active)
            .OrderBy(m => m.PurchasedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        return mineId;
    }

    public async Task<PlayerProfileResponse?> GetProfileAsync(Guid playerId, Guid viewerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        await profileUpgrader.EnsurePlayerUpgradedAsync(player, ct);
        return await MapProfileAsync(player, viewerId, ct);
    }

    public async Task<PlayerProfileResponse?> GetProfileByUsernameAsync(
        string username,
        Guid viewerId,
        CancellationToken ct)
    {
        var reporter = OffworldNewsReporterSocial.TryGetByUsername(username);
        if (reporter is not null)
        {
            var (status, friendshipId) =
                await reporterFriendshipService.GetFriendshipStatusAsync(viewerId, reporter.Slug, ct);
            return OffworldNewsReporterProfileMapper.ToPlayerProfile(reporter, status, friendshipId, hostingPaths);
        }

        var normalized = username.Trim().ToLowerInvariant();
        var player = await db.Players.FirstOrDefaultAsync(p => p.Username.ToLower() == normalized, ct);
        if (player is null)
        {
            return null;
        }

        await profileUpgrader.EnsurePlayerUpgradedAsync(player, ct);
        return await MapProfileAsync(player, viewerId, ct);
    }

    public async Task<(PlayerProfileResponse? Profile, string? Error)> UpdateProfileAsync(
        Guid playerId,
        UpdatePlayerProfileRequest request,
        CancellationToken ct)
    {
        var error = ProfileValidator.ValidateUpdate(
            request.Mood ?? string.Empty,
            request.AboutMe ?? string.Empty,
            request.Music ?? string.Empty,
            request.Interests ?? string.Empty,
            request.Discord ?? string.Empty,
            request.Bluesky ?? string.Empty,
            request.Twitter ?? string.Empty,
            request.Youtube ?? string.Empty,
            request.Facebook ?? string.Empty);

        if (error is not null)
        {
            return (null, error);
        }

        if (request.ProfileGender is not null || request.ProfilePreferredPronouns is not null)
        {
            var genderError = ProfileValidator.ValidateGenderAndPronouns(
                request.ProfileGender ?? string.Empty,
                request.ProfilePreferredPronouns);
            if (genderError is not null)
            {
                return (null, genderError);
            }
        }

        if (request.ProfileLocale is not null && !ProfileLocale.IsValid(request.ProfileLocale))
        {
            return (null, "Choose a supported interface language.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        player.ProfileMood = string.IsNullOrWhiteSpace(request.Mood)
            ? PlayerProfileDefaults.Mood
            : request.Mood.Trim();
        player.ProfileAboutMe = request.AboutMe?.Trim() ?? string.Empty;
        player.ProfileMusic = request.Music?.Trim() ?? string.Empty;
        player.ProfileInterests = request.Interests?.Trim() ?? string.Empty;
        player.ProfileDiscord = request.Discord?.Trim() ?? string.Empty;
        player.ProfileBluesky = request.Bluesky?.Trim() ?? string.Empty;
        player.ProfileTwitter = request.Twitter?.Trim() ?? string.Empty;
        player.ProfileYoutube = request.Youtube?.Trim() ?? string.Empty;
        player.ProfileFacebook = request.Facebook?.Trim() ?? string.Empty;

        if (request.ProfileGender is not null)
        {
            player.ProfileGender = ProfileGender.Normalize(request.ProfileGender);
            if (ProfileGender.RequiresPreferredPronouns(player.ProfileGender))
            {
                player.ProfilePreferredPronouns = ProfilePreferredPronouns.Normalize(
                    request.ProfilePreferredPronouns);
            }
            else
            {
                player.ProfilePreferredPronouns = string.Empty;
            }

            if (!ProfileAvatarPresets.HasCustomUpload(player.ProfileImageUrl))
            {
                player.ProfileAvatarPreset = ProfileAvatarPresets.FromGender(player.ProfileGender);
            }
        }
        else if (request.ProfilePreferredPronouns is not null &&
                 ProfileGender.RequiresPreferredPronouns(player.ProfileGender))
        {
            player.ProfilePreferredPronouns = ProfilePreferredPronouns.Normalize(
                request.ProfilePreferredPronouns);
        }

        if (request.ProfileLocale is not null)
        {
            var normalizedLocale = ProfileLocale.Normalize(request.ProfileLocale);
            if (string.IsNullOrEmpty(normalizedLocale))
            {
                return (null, "Choose a supported interface language.");
            }

            player.ProfileLocale = normalizedLocale;
        }

        if (request.ProfileBirthdayPublic is not null)
        {
            player.ProfileBirthdayPublic = request.ProfileBirthdayPublic.Value;
        }

        if (request.ProfileAgePublic is not null)
        {
            player.ProfileAgePublic = request.ProfileAgePublic.Value;
        }

        if (request.ProfileSpecies is not null)
        {
            var speciesError = PlayerProfileSpecies.Validate(request.ProfileSpecies);
            if (speciesError is not null)
            {
                return (null, speciesError);
            }

            player.ProfileSpecies = PlayerProfileSpecies.Normalize(request.ProfileSpecies);
        }

        await profileUpgrader.EnsurePlayerUpgradedAsync(player, ct);
        await ResolveActiveProfileFlagsAsync(playerId, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public async Task<(PlayerProfileResponse? Profile, string? Error)> SubmitJobApplicationAsync(
        Guid playerId,
        SubmitJobApplicationRequest request,
        CancellationToken ct)
    {
        var job = PlayerJobCatalog.TryGet(request.JobSlug);
        if (job is null)
        {
            return (null, "Choose a position from the employment catalog.");
        }

        var error = ProfileValidator.ValidateJobApplication(
            request.Mood ?? string.Empty,
            request.AboutMe ?? string.Empty,
            request.Interests ?? string.Empty,
            request.Music ?? string.Empty,
            request.Discord ?? string.Empty,
            request.Bluesky ?? string.Empty,
            request.Twitter ?? string.Empty,
            request.Youtube ?? string.Empty,
            request.Facebook ?? string.Empty,
            request.ProfileSpecies);

        if (error is not null)
        {
            return (null, error);
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        if (player.JobApplicationCompletedAt is not null)
        {
            return (null, "Job application already submitted.");
        }

        player.ProfileSpecies = PlayerProfileSpecies.Normalize(request.ProfileSpecies);
        player.ProfileMood = string.IsNullOrWhiteSpace(request.Mood)
            ? PlayerProfileDefaults.Mood
            : request.Mood.Trim();
        player.ProfileAboutMe = request.AboutMe?.Trim() ?? string.Empty;
        player.ProfileInterests = request.Interests?.Trim() ?? string.Empty;
        player.ProfileMusic = request.Music?.Trim() ?? string.Empty;
        player.ProfileDiscord = request.Discord?.Trim() ?? string.Empty;
        player.ProfileBluesky = request.Bluesky?.Trim() ?? string.Empty;
        player.ProfileTwitter = request.Twitter?.Trim() ?? string.Empty;
        player.ProfileYoutube = request.Youtube?.Trim() ?? string.Empty;
        player.ProfileFacebook = request.Facebook?.Trim() ?? string.Empty;
        player.JobApplicationCompletedAt = DateTime.UtcNow;

        db.PlayerJobHistory.Add(new PlayerJobHistoryEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            JobSlug = job.Slug,
            JobTitle = job.Title,
            IsCurrent = true,
            StartedAtUtc = DateTime.UtcNow,
        });

        await profileUpgrader.EnsurePlayerUpgradedAsync(player, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public static PlayerJobCatalogResponse GetJobCatalog() =>
        new(PlayerJobCatalog.All
            .Select(job => new PlayerJobCatalogEntryDto(job.Slug, job.Title, job.WorkspaceModule, job.Description))
            .ToList());

    public static PlayerSpeciesCatalogResponse GetSpeciesCatalog() =>
        new(PlayerProfileSpecies.Catalog);

    public async Task<(PlayerProfileResponse? Profile, string? Error)> UploadProfileAvatarAsync(
        Guid playerId,
        Stream content,
        string contentType,
        long length,
        CancellationToken ct)
    {
        var header = new byte[16];
        var read = await content.ReadAsync(header.AsMemory(0, header.Length), ct);
        var validationError = ProfileAvatarValidator.Validate(contentType, length, header.AsSpan(0, read));
        if (validationError is not null)
        {
            return (null, validationError);
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }
        else
        {
            return (null, "Could not read uploaded image.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        player.ProfileImageUrl = await profileAvatarStorage.SaveAsync(playerId, content, contentType, ct);
        player.ProfileImageRevision++;
        await ResolveActiveProfileFlagsAsync(playerId, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public async Task<(PlayerProfileResponse? Profile, string? Error)> UploadCompanyLogoAsync(
        Guid playerId,
        Stream content,
        string contentType,
        CancellationToken ct)
    {
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);
        var bytes = buffered.ToArray();
        var validationError = CompanyLogoValidator.Validate(contentType, bytes);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        var mine = await db.Mines.FirstOrDefaultAsync(
            m => m.PlayerId == playerId && m.Status == MineStatus.Active,
            ct);
        if (mine is null)
        {
            return (null, "You need an active mine before uploading a company logo.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        buffered.Position = 0;
        mine.CompanyLogoUrl = await companyLogoStorage.SaveAsync(mine.Id, buffered, ct);
        mine.CompanyLogoRevision++;
        mine.CompanyLogoIsCustom = true;
        await companyLogoQueueService.CancelPendingForMineAsync(mine.Id, ct);
        await ResolveActiveProfileFlagsAsync(playerId, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public async Task<(CompanyLogoGenerationActionResponse? Result, string? Error)> EnqueueCompanyLogoGenerationAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var (success, message, status) = await companyLogoQueueService.EnqueueForPlayerAsync(playerId, ct);
        return (
            new CompanyLogoGenerationActionResponse(
                success ? "queued" : "error",
                message,
                status),
            success ? null : message);
    }

    public async Task<CompanyLogoGenerationStatusDto?> GetCompanyLogoGenerationStatusAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var mine = await db.Mines.AsNoTracking()
            .FirstOrDefaultAsync(m => m.PlayerId == playerId && m.Status == MineStatus.Active, ct);
        if (mine is null)
        {
            return null;
        }

        return await companyLogoQueueService.GetStatusForMineAsync(mine.Id, ct);
    }

    public async Task<(PlayerProfileResponse? Profile, string? Error)> UploadProfileBackgroundAsync(
        Guid playerId,
        Stream content,
        string contentType,
        long length,
        CancellationToken ct)
    {
        var header = new byte[16];
        var read = await content.ReadAsync(header.AsMemory(0, header.Length), ct);
        var validationError = ProfileBackgroundValidator.Validate(contentType, length, header.AsSpan(0, read));
        if (validationError is not null)
        {
            return (null, validationError);
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }
        else
        {
            return (null, "Could not read uploaded image.");
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        player.ProfileBackgroundUrl = await profileBackgroundStorage.SaveAsync(playerId, content, contentType, ct);
        player.ProfileBackgroundRevision++;
        await ResolveActiveProfileFlagsAsync(playerId, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public async Task<(PlayerProfileResponse? Profile, string? Error)> RemoveProfileBackgroundAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(player.ProfileBackgroundUrl))
        {
            return (await MapProfileAsync(player, playerId, ct), null);
        }

        await profileBackgroundStorage.DeleteForPlayerAsync(playerId, ct);
        player.ProfileBackgroundUrl = string.Empty;
        player.ProfileBackgroundRevision++;
        await ResolveActiveProfileFlagsAsync(playerId, ct);
        await db.SaveChangesAsync(ct);

        return (await MapProfileAsync(player, playerId, ct), null);
    }

    public async Task<FriendsListResponse> GetFriendsAsync(Guid playerId, CancellationToken ct)
    {
        var friendships = await db.Friendships.AsNoTracking()
            .Where(f => f.PlayerId == playerId || f.FriendId == playerId)
            .ToListAsync(ct);

        var otherIds = friendships
            .Select(f => f.PlayerId == playerId ? f.FriendId : f.PlayerId)
            .Distinct()
            .ToList();

        var players = otherIds.Count == 0
            ? new Dictionary<Guid, PlayerEntity>()
            : await db.Players.AsNoTracking()
                .Where(p => otherIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

        var friends = new List<FriendSummaryDto>();
        var incoming = new List<FriendSummaryDto>();
        var outgoing = new List<FriendSummaryDto>();

        foreach (var friendship in friendships)
        {
            var otherId = friendship.PlayerId == playerId ? friendship.FriendId : friendship.PlayerId;
            if (!players.TryGetValue(otherId, out var other))
            {
                continue;
            }

            var summary = new FriendSummaryDto(
                friendship.Id,
                other.Id,
                other.Username,
                other.ProfileNumber,
                other.ProfileMood,
                friendship.Status,
                friendship.Status == FriendshipStatuses.Accepted
                    ? friendship.AcceptedAt ?? friendship.CreatedAt
                    : friendship.CreatedAt);

            if (friendship.Status == FriendshipStatuses.Accepted)
            {
                friends.Add(summary);
            }
            else if (friendship.FriendId == playerId)
            {
                incoming.Add(summary);
            }
            else
            {
                outgoing.Add(summary);
            }
        }

        var reporterFriends = await reporterFriendshipService.GetFriendSummariesAsync(playerId, ct);
        var mergedFriends = friends
            .Concat(reporterFriends)
            .OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var viewer = await db.Players.AsNoTracking()
            .Where(p => p.Id == playerId)
            .Select(p => new { p.Username, p.AdminTestingModeEnabled })
            .FirstOrDefaultAsync(ct);

        if (viewer is not null && viewer.AdminTestingModeEnabled)
        {
            mergedFriends = TestingDummyFriends.MergeFriendSummaries(mergedFriends).ToList();
        }

        return new FriendsListResponse(
            mergedFriends,
            incoming.OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase).ToList(),
            outgoing.OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<(bool Success, string Message)> AddFriendByProfileNumberAsync(
        Guid playerId,
        string profileNumberInput,
        CancellationToken ct)
    {
        var normalized = ProfileNumberNormalizer.Normalize(profileNumberInput);
        if (normalized is null)
        {
            return (false, ProfileNumberFormats.ValidationMessage);
        }

        var reporter = OffworldNewsReporterSocial.TryGetByProfileNumber(normalized);
        if (reporter is not null)
        {
            return await reporterFriendshipService.AddFriendAsync(playerId, reporter.Slug, ct);
        }

        var target = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProfileNumber == normalized, ct);
        if (target is null)
        {
            return (false, "No profile found with that number.");
        }

        if (target.Id == playerId)
        {
            return (false, "You cannot friend yourself.");
        }

        var existing = await db.Friendships.FirstOrDefaultAsync(
            f => (f.PlayerId == playerId && f.FriendId == target.Id) ||
                 (f.PlayerId == target.Id && f.FriendId == playerId),
            ct);

        if (existing is not null)
        {
            if (existing.Status == FriendshipStatuses.Accepted)
            {
                return (false, $"You are already friends with {target.Username}.");
            }

            if (existing.PlayerId == playerId)
            {
                return (false, $"Friend request already sent to {target.Username}.");
            }

            return (false, $"{target.Username} already sent you a request. Accept it from your friends list.");
        }

        db.Friendships.Add(new FriendshipEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            FriendId = target.Id,
            Status = FriendshipStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, target.Id, LiveUpdateScopes.Friends);
        return (true, $"Friend request sent to {target.Username}.");
    }

    public async Task<(bool Success, string Message)> AcceptFriendAsync(
        Guid playerId,
        Guid friendshipId,
        CancellationToken ct)
    {
        var friendship = await db.Friendships.FirstOrDefaultAsync(
            f => f.Id == friendshipId &&
                 f.FriendId == playerId &&
                 f.Status == FriendshipStatuses.Pending,
            ct);

        if (friendship is null)
        {
            return (false, "Friend request not found.");
        }

        friendship.Status = FriendshipStatuses.Accepted;
        friendship.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, playerId, LiveUpdateScopes.Friends);
        LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, friendship.PlayerId, LiveUpdateScopes.Friends);
        return (true, "Friend request accepted.");
    }

    public async Task<(bool Success, string Message)> RemoveFriendAsync(
        Guid playerId,
        Guid friendshipId,
        CancellationToken ct)
    {
        var reporterLink = await db.ReporterFriendships.AsNoTracking()
            .AnyAsync(f => f.Id == friendshipId && f.PlayerId == playerId, ct);
        if (reporterLink)
        {
            return await reporterFriendshipService.RemoveFriendAsync(playerId, friendshipId, ct);
        }

        var friendship = await db.Friendships.FirstOrDefaultAsync(
            f => f.Id == friendshipId && (f.PlayerId == playerId || f.FriendId == playerId),
            ct);

        if (friendship is null)
        {
            return (false, "Friendship not found.");
        }

        var wasPending = friendship.Status == FriendshipStatuses.Pending;
        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync(ct);

        return (true, wasPending ? "Friend request removed." : "Friend removed.");
    }

    private async Task<PlayerProfileResponse> MapProfileAsync(
        PlayerEntity player,
        Guid viewerId,
        CancellationToken ct)
    {
        var mine = await LoadPlayerMineAsync(player.Id, ct, activeOnly: true);
        var (friendshipStatus, friendshipId) = await GetFriendshipStatusAsync(viewerId, player.Id, ct);
        var isOwner = player.Id == viewerId;
        ProfileFlagDto? activeFlag = null;
        if (isOwner)
        {
            activeFlag = await GetActiveProfileFlagAsync(player.Id, ct);
        }

        var friends = await GetProfileFriendsAsync(player.Id, ct);
        if (isOwner && player.AdminTestingModeEnabled)
        {
            friends = TestingDummyFriends.MergeProfileFriends(friends);
        }

        Guid? mineId = null;
        bool companyNameListed = false;
        Guid? companyNameListingId = null;
        decimal? companyNameListingPrice = null;

        if (isOwner && mine is not null)
        {
            mineId = mine.Id;
            (companyNameListingId, companyNameListingPrice) =
                await companyNameService.GetActiveListingForPlayerAsync(player.Id, ct);
            companyNameListed = companyNameListingId is not null;
        }

        var logoGeneration = await ResolveCompanyLogoGenerationAsync(mine, ct);
        var pronouns = MapPronouns(player);
        var completion = isOwner
            ? ProfileCompletionEvaluator.Evaluate(
                player.ProfileGender,
                player.ProfilePreferredPronouns,
                player.ProfileLocale)
            : new ProfileCompletionStatus(false, []);
        var today = UtcGameClock.Today;
        var publicBirthday = BirthdayHelper.TryFormatPublicBirthday(
            player.Birthday,
            player.ProfileBirthdayPublic);
        var publicAge = BirthdayHelper.TryComputePublicAge(
            player.Birthday,
            player.ProfileAgePublic,
            today);
        var jobHistory = await LoadJobHistoryAsync(player.Id, ct);
        var currentJob = jobHistory.FirstOrDefault(entry => entry.IsCurrent);
        var jobApplicationRequired = isOwner && JobApplicationEvaluator.IsRequired(player.JobApplicationCompletedAt);

        return new PlayerProfileResponse(
            player.Id,
            player.Username,
            player.ProfileNumber,
            ProfileAvatarPresets.ResolveDisplayUrl(
                player.ProfileImageUrl,
                player.ProfileImageRevision,
                player.ProfileAvatarPreset),
            FormatProfileImageUrl(player.ProfileBackgroundUrl, player.ProfileBackgroundRevision),
            FormatProfileImageUrl(mine?.CompanyLogoUrl ?? string.Empty, mine?.CompanyLogoRevision ?? 0),
            player.ProfileMood,
            player.ProfileAboutMe,
            player.ProfileMusic,
            player.ProfileInterests,
            player.ProfileDiscord,
            player.ProfileBluesky,
            player.ProfileTwitter,
            player.ProfileYoutube,
            player.ProfileFacebook,
            player.CreatedAt,
            player.CurrentGameDay,
            player.Credits,
            mine?.Name ?? "No active mine",
            mine?.Workers.Count ?? 0,
            mine?.Zones.Count ?? 0,
            isOwner,
            friendshipStatus,
            friendshipId?.ToString() ?? string.Empty,
            activeFlag,
            friends,
            mineId,
            companyNameListed,
            companyNameListingId,
            companyNameListingPrice,
            CompanyLogoGenerationStatus: logoGeneration.Status,
            CompanyLogoGenerationMessage: logoGeneration.Message,
            CompanyLogoAiEnabled: companyLogoQueueService.IsAiEnabled,
            ProfileAvatarPreset: ProfileAvatarPresets.Normalize(player.ProfileAvatarPreset),
            HasCustomProfilePhoto: ProfileAvatarPresets.HasCustomUpload(player.ProfileImageUrl),
            ProfileGender: player.ProfileGender,
            ProfilePreferredPronouns: player.ProfilePreferredPronouns,
            ProfileLocale: player.ProfileLocale,
            PronounSubject: pronouns.Subject,
            PronounObject: pronouns.Object,
            PronounPossessive: pronouns.Possessive,
            PronounLabel: pronouns.Label,
            RequiresPreferredPronouns: ProfileGender.RequiresPreferredPronouns(player.ProfileGender),
            ProfileCompletionRequired: completion.Required,
            MissingProfileFields: completion.MissingFields,
            ProfileBirthdayPublic: isOwner && player.ProfileBirthdayPublic,
            ProfileAgePublic: isOwner && player.ProfileAgePublic,
            PublicBirthday: publicBirthday,
            PublicAge: publicAge,
            IsStaffAdmin: AdminOptions.IsAdminUsername(player.Username),
            IsStaffModerator: !AdminOptions.IsAdminUsername(player.Username)
                && ModeratorOptions.IsModeratorUsername(player.Username),
            TestingModeEnabled: isOwner && player.AdminTestingModeEnabled,
            JobApplicationRequired: jobApplicationRequired,
            CurrentJob: currentJob,
            JobHistory: jobHistory,
            ProfileSpecies: player.ProfileSpecies,
            ProfileSpeciesLabel: PlayerProfileSpecies.DisplayLabel(player.ProfileSpecies));
    }

    private async Task<IReadOnlyList<PlayerJobHistoryEntryDto>> LoadJobHistoryAsync(
        Guid playerId,
        CancellationToken ct)
    {
        var rows = await db.PlayerJobHistory.AsNoTracking()
            .Where(j => j.PlayerId == playerId)
            .ToListAsync(ct);

        return PlayerJobHistoryMapper.OrderForDisplay(rows.Select(row =>
            PlayerJobHistoryMapper.ToDto(
                row.JobSlug,
                row.JobTitle,
                row.IsCurrent,
                row.StartedAtUtc,
                row.EndedAtUtc)));
    }

    private static ProfilePronounSet MapPronouns(PlayerEntity player) =>
        ProfilePronouns.Resolve(player.ProfileGender, player.ProfilePreferredPronouns);

    private async Task<CompanyLogoGenerationStatusDto> ResolveCompanyLogoGenerationAsync(
        MineEntity? mine,
        CancellationToken ct)
    {
        if (mine is null)
        {
            return new CompanyLogoGenerationStatusDto("none", string.Empty, null, null, null);
        }

        return await companyLogoQueueService.GetStatusForMineAsync(mine.Id, ct);
    }

    private async Task<IReadOnlyList<ProfileFriendDto>> GetProfileFriendsAsync(Guid playerId, CancellationToken ct)
    {
        var friendships = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatuses.Accepted &&
                        (f.PlayerId == playerId || f.FriendId == playerId))
            .ToListAsync(ct);

        var friends = new List<ProfileFriendDto>();
        if (friendships.Count > 0)
        {
            var otherIds = friendships
                .Select(f => f.PlayerId == playerId ? f.FriendId : f.PlayerId)
                .Distinct()
                .ToList();

            var players = await db.Players.AsNoTracking()
                .Where(p => otherIds.Contains(p.Id))
                .ToListAsync(ct);

            friends.AddRange(players.Select(p => new ProfileFriendDto(
                p.Id,
                p.Username,
                p.ProfileNumber,
                p.ProfileMood,
                string.Empty)));
        }

        foreach (var reporter in await reporterFriendshipService.GetFriendSummariesAsync(playerId, ct))
        {
            friends.Add(new ProfileFriendDto(
                Guid.Empty,
                reporter.Username,
                reporter.ProfileNumber,
                reporter.Mood,
                string.Empty,
                IsReporter: true,
                ReporterSlug: reporter.ReporterSlug));
        }

        return friends
            .OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<ProfileFlagDto?> GetActiveProfileFlagAsync(Guid playerId, CancellationToken ct)
    {
        var flag = await db.ProfileFlags.AsNoTracking()
            .Where(f => f.PlayerId == playerId && f.ResolvedAt == null)
            .OrderByDescending(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (flag is null)
        {
            return null;
        }

        return new ProfileFlagDto(
            flag.Id,
            flag.FlaggedByUsername,
            flag.Comment,
            flag.CreatedAt,
            flag.ResolvedAt);
    }

    private async Task ResolveActiveProfileFlagsAsync(Guid playerId, CancellationToken ct)
    {
        var activeFlags = await db.ProfileFlags
            .Where(f => f.PlayerId == playerId && f.ResolvedAt == null)
            .ToListAsync(ct);

        if (activeFlags.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var flag in activeFlags)
        {
            flag.ResolvedAt = now;
        }
    }

    private async Task<(string Status, Guid? FriendshipId)> GetFriendshipStatusAsync(
        Guid viewerId,
        Guid profilePlayerId,
        CancellationToken ct)
    {
        if (viewerId == profilePlayerId)
        {
            return ("self", null);
        }

        var link = await db.Friendships.AsNoTracking().FirstOrDefaultAsync(
            f => (f.PlayerId == viewerId && f.FriendId == profilePlayerId) ||
                 (f.PlayerId == profilePlayerId && f.FriendId == viewerId),
            ct);

        if (link is null)
        {
            return ("none", null);
        }

        if (link.Status == FriendshipStatuses.Accepted)
        {
            return ("accepted", link.Id);
        }

        return link.PlayerId == viewerId
            ? ("pending_outgoing", link.Id)
            : ("pending_incoming", link.Id);
    }

    private static string FormatProfileImageUrl(string url, int revision)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return revision > 0 ? $"{url}?v={revision}" : url;
    }

    private async Task<(DayAdvanceResponse? Report, IReadOnlyList<EventCompletionDto> Completions)> ProcessDueDaysAsync(
        PlayerEntity player, CancellationToken ct)
    {
        var gate = DayProcessLocks.GetOrAdd(player.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await db.Entry(player).ReloadAsync(ct);
            await EnsureLastProcessedUtcDateAsync(player, ct);

            var dayBefore = player.CurrentGameDay;
            var todayUtc = UtcGameClock.Today;
            DayAdvanceResponse? latestReport = null;

            while (player.LastProcessedUtcDate < todayUtc)
            {
                latestReport = await AdvanceSingleDayAsync(player, ct);
                player.LastProcessedUtcDate = player.LastProcessedUtcDate.AddDays(1);
                await db.SaveChangesAsync(ct);
            }

            await db.Entry(player).ReloadAsync(ct);
            var daysAdvanced = player.CurrentGameDay - dayBefore;
            if (daysAdvanced <= 0)
            {
                return (latestReport, []);
            }

            var completions = await specialEventService.RecordProgressAsync(
                player.Id,
                SpecialEventChallengeType.AdvanceDay,
                null,
                daysAdvanced,
                ct);
            LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, player.Id, LiveUpdateScopes.Mine);
            LiveUpdatePublisher.NotifyPlayerRefresh(liveUpdateBroadcaster, player.Id, LiveUpdateScopes.Reserve);
            return (latestReport, completions);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureLastProcessedUtcDateAsync(PlayerEntity player, CancellationToken ct)
    {
        if (player.LastProcessedUtcDate != default)
        {
            return;
        }

        player.LastProcessedUtcDate = DateOnly.FromDateTime(
            player.CreatedAt.Kind == DateTimeKind.Utc
                ? player.CreatedAt
                : player.CreatedAt.ToUniversalTime());
        await db.SaveChangesAsync(ct);
    }

    private async Task<DayAdvanceResponse> AdvanceSingleDayAsync(PlayerEntity player, CancellationToken ct)
    {
        var playerId = player.Id;
        var mine = await LoadPlayerMineAsync(playerId, ct, activeOnly: true);
        if (mine is null)
        {
            throw new InvalidOperationException("No active mine found for player.");
        }

        var inventory = await db.Inventory.Where(i => i.PlayerId == playerId).ToListAsync(ct);
        var inventoryStates = inventory.Select(EntityMapper.ToState).ToList();
        await MigrateOreInventoryToStockpileAsync(playerId, mine.Id, inventory, ct);

        var stockpileEntities = await db.MineOreStockpile.Where(s => s.MineId == mine.Id).ToListAsync(ct);
        var stockpileStates = stockpileEntities.Select(EntityMapper.ToState).ToList();

        var nextDay = player.CurrentGameDay + 1;
        var marketUtcDate = player.LastProcessedUtcDate.AddDays(1);
        var market = await GetOrCreateMarketSnapshotAsync(nextDay, marketUtcDate, ct);

        var playerState = EntityMapper.ToState(player);
        var mineState = EntityMapper.ToState(mine);
        var result = simulation.AdvanceDay(playerState, mineState, inventoryStates, stockpileStates, market);

        player.Credits = playerState.Credits;
        player.CurrentGameDay = playerState.CurrentGameDay;

        foreach (var zoneState in mineState.Zones)
        {
            var zoneEntity = mine.Zones.First(z => z.Id == zoneState.Id);
            EntityMapper.ApplyZone(zoneEntity, zoneState);
        }

        foreach (var state in inventoryStates)
        {
            var entity = inventory.FirstOrDefault(e => e.Id == state.Id);
            if (entity is null)
            {
                db.Inventory.Add(EntityMapper.ToEntity(state));
                continue;
            }

            EntityMapper.ApplyInventory(entity, state);
            if (entity.Quantity <= 0 && entity.BrokenQuantity <= 0)
            {
                db.Inventory.Remove(entity);
            }
        }

        foreach (var state in stockpileStates)
        {
            var entity = stockpileEntities.FirstOrDefault(e => e.Id == state.Id);
            if (entity is null)
            {
                db.MineOreStockpile.Add(EntityMapper.ToEntity(state));
                continue;
            }

            EntityMapper.ApplyStockpile(entity, state);
            if (entity.Quantity <= 0)
            {
                db.MineOreStockpile.Remove(entity);
            }
        }

        var shipmentMessages = await oreShipmentService.ProcessDayAsync(playerId, mine.Id, result.NewGameDay, ct);
        result.Messages.AddRange(shipmentMessages);

        if (result.PayrollPaid > 0)
        {
            player.ReserveBalance -= result.PayrollPaid;
            db.ReserveTransactions.Add(new ReserveTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = ReserveTransactionType.MinePayroll,
                Amount = -result.PayrollPaid,
                Description = "Daily mine payroll",
                GameDay = result.NewGameDay
            });
        }

        var currentJobSlug = await db.PlayerJobHistory.AsNoTracking()
            .Where(j => j.PlayerId == playerId && j.IsCurrent)
            .Select(j => j.JobSlug)
            .FirstOrDefaultAsync(ct);
        var jobSalary = CosmicReserveService.ResolveDailyJobSalary(currentJobSlug);
        if (jobSalary > 0)
        {
            player.ReserveBalance += jobSalary;
            result.JobSalaryPaid = jobSalary;
            db.ReserveTransactions.Add(new ReserveTransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = ReserveTransactionType.JobSalary,
                Amount = jobSalary,
                Description = "Daily job salary",
                GameDay = result.NewGameDay
            });
        }

        var estimatedIncome = simulation.CalculateEstimatedDailyIncome(mineState, inventoryStates);
        var obligationPreview = CompanyObligationsCalculator.ComputePreview(
            mine.Workers.Count,
            estimatedIncome,
            result.NewGameDay,
            mine.MiningRightsPaidThroughDay);

        var obligations = obligationPreview;
        if (result.NewGameDay > mine.MiningRightsPaidThroughDay
            && player.ReserveBalance >= obligationPreview.Total + GameBalance.MiningRightsRenewalFee)
        {
            obligations = CompanyObligationsCalculator.ComputeDaily(
                mine.Workers.Count,
                estimatedIncome,
                result.NewGameDay,
                mine.MiningRightsPaidThroughDay,
                attemptAutoRenewal: true);
        }
        else if (result.NewGameDay > mine.MiningRightsPaidThroughDay)
        {
            obligations.Messages.Add("Could not auto-renew mining rights — insufficient Cosmic Reserve.");
        }

        ApplyCompanyObligations(player, mine, obligations, playerId, result.NewGameDay, result.Messages);
        result.ReserveBalance = player.ReserveBalance;

        if (result.SuppliesConsumed > 0)
        {
            db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Type = TransactionType.DayAdvance,
                Amount = -result.SuppliesConsumed,
                Description = "Daily supply consumption",
                GameDay = result.NewGameDay
            });
        }

        var world = await db.GameWorld.FirstAsync(ct);
        world.CurrentDay = Math.Max(world.CurrentDay, result.NewGameDay);
        world.LastTickAt = DateTime.UtcNow;

        return new DayAdvanceResponse(
            result.NewGameDay,
            result.Credits,
            result.ReserveBalance,
            result.OreExtracted.Select(kv => new OreExtractedDto(kv.Key, kv.Value)).ToList(),
            result.PayrollPaid,
            result.JobSalaryPaid,
            result.SuppliesConsumed,
            MapMarket(result.MarketSnapshot, await specialEventService.GetActiveMarketBonusesAsync(ct)),
            result.Messages);
    }

    public async Task<ShippingDashboardResponse> GetShippingDashboardForMineAsync(
        Guid playerId, Guid mineId, CancellationToken ct)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return new ShippingDashboardResponse(0, 0, false, [], [], []);
        }

        return await oreShipmentService.GetDashboardAsync(playerId, mineId, player.CurrentGameDay, ct);
    }

    public Task<(bool Success, string Message, Guid? ShipmentId)> ScheduleShipmentAsync(
        Guid playerId, Guid mineId, ScheduleShipmentRequest request, CancellationToken ct) =>
        oreShipmentService.ScheduleShipmentAsync(playerId, mineId, request, ct);

    public Task<(bool Success, string Message)> CancelShipmentAsync(
        Guid playerId, Guid mineId, Guid shipmentId, CancellationToken ct) =>
        oreShipmentService.CancelShipmentAsync(playerId, mineId, shipmentId, ct);

    private async Task MigrateOreInventoryToStockpileAsync(
        Guid playerId,
        Guid mineId,
        List<InventoryItemEntity> inventory,
        CancellationToken ct)
    {
        var oreItems = inventory.Where(i => i.Category == ItemCategory.Ore && i.Quantity > 0).ToList();
        if (oreItems.Count == 0)
        {
            return;
        }

        foreach (var ore in oreItems)
        {
            var stock = await db.MineOreStockpile.FirstOrDefaultAsync(
                s => s.MineId == mineId && s.OreType == ore.ItemType, ct);

            if (stock is null)
            {
                db.MineOreStockpile.Add(new MineOreStockpileEntity
                {
                    Id = Guid.NewGuid(),
                    MineId = mineId,
                    OreType = ore.ItemType,
                    Quantity = ore.Quantity,
                    Condition = ore.Condition,
                });
            }
            else
            {
                stock.Condition = ItemConditionCalculator.MergeCondition(
                    stock.Quantity, stock.Condition, ore.Quantity, ore.Condition);
                stock.Quantity += ore.Quantity;
            }

            db.Inventory.Remove(ore);
            inventory.Remove(ore);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<MineEntity?> LoadMineAsync(Guid mineId, CancellationToken ct, bool activeOnly = false)
    {
        var query = db.Mines
            .Include(m => m.Zones)
            .Include(m => m.Workers)
            .Where(m => m.Id == mineId);

        if (activeOnly)
        {
            query = query.Where(m => m.Status == MineStatus.Active);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<MineEntity?> LoadPlayerMineAsync(Guid playerId, CancellationToken ct, bool activeOnly)
    {
        var query = db.Mines
            .Include(m => m.Zones)
            .Include(m => m.Workers)
            .Where(m => m.PlayerId == playerId);

        if (activeOnly)
        {
            query = query.Where(m => m.Status == MineStatus.Active);
        }

        return await query.OrderBy(m => m.PurchasedAt).FirstOrDefaultAsync(ct);
    }

    private async Task<DailyMarketSnapshot> GetOrCreateMarketSnapshotAsync(
        int gameDay,
        DateOnly utcDate,
        CancellationToken ct)
    {
        var existing = await db.MarketPriceHistory.AsNoTracking()
            .Where(m => m.GameDay == gameDay)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            return new DailyMarketSnapshot
            {
                GameDay = gameDay,
                Date = existing[0].UtcDate ?? utcDate,
                Source = existing[0].Source,
                Prices = existing.Select(e => new MarketPriceEntry
                {
                    SupplyType = e.SupplyType,
                    Price = e.Price,
                    ChangePct = e.ChangePct
                }).ToList()
            };
        }

        var snapshot = await marketProvider.GetDailyPricesAsync(gameDay, utcDate, ct);

        foreach (var price in snapshot.Prices)
        {
            db.MarketPriceHistory.Add(new MarketPriceHistoryEntity
            {
                Id = Guid.NewGuid(),
                GameDay = gameDay,
                UtcDate = utcDate,
                SupplyType = price.SupplyType,
                Price = price.Price,
                ChangePct = price.ChangePct,
                Source = snapshot.Source
            });
        }

        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    private async Task<string?> TryGrantBirthdayBonusAsync(PlayerEntity player, CancellationToken ct)
    {
        if (player.Birthday is null)
        {
            return null;
        }

        var today = UtcGameClock.Today;
        if (!BirthdayHelper.IsBirthdayToday(player.Birthday.Value, today))
        {
            return null;
        }

        if (player.LastBirthdayBonusYear == today.Year)
        {
            return null;
        }

        player.ReserveBalance += Credits.BirthdayBonus;
        player.LastBirthdayBonusYear = today.Year;
        player.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Type = ReserveTransactionType.BirthdayBonus,
            Amount = Credits.BirthdayBonus,
            Description = "Happy birthday bonus",
            GameDay = player.CurrentGameDay
        });

        await db.SaveChangesAsync(ct);
        return $"Happy birthday, {player.Username}! You received {Credits.BirthdayBonus:0} bonus credits.";
    }

    private void ApplyCompanyObligations(
        PlayerEntity player,
        MineEntity mine,
        CompanyObligationsResult obligations,
        Guid playerId,
        int gameDay,
        List<string> messages)
    {
        foreach (var message in obligations.Messages)
        {
            messages.Add(message);
        }

        if (obligations.NewPaidThroughDay.HasValue)
        {
            mine.MiningRightsPaidThroughDay = obligations.NewPaidThroughDay.Value;
        }

        AddReserveObligation(player, playerId, gameDay, obligations.CompanyTax,
            ReserveTransactionType.CompanyTax, "Daily company tax");
        AddReserveObligation(player, playerId, gameDay, obligations.HealthInsurance,
            ReserveTransactionType.HealthInsurance, "Health insurance");
        AddReserveObligation(player, playerId, gameDay, obligations.JobInsurance,
            ReserveTransactionType.JobInsurance, "Job insurance");
        AddReserveObligation(player, playerId, gameDay, obligations.BeltFee,
            ReserveTransactionType.BeltFee, "Belt operating fee");
        AddReserveObligation(player, playerId, gameDay, obligations.MiningRights,
            ReserveTransactionType.MiningRights, "Mining rights");
    }

    private void AddReserveObligation(
        PlayerEntity player,
        Guid playerId,
        int gameDay,
        decimal amount,
        ReserveTransactionType type,
        string description)
    {
        if (amount <= 0)
        {
            return;
        }

        player.ReserveBalance -= amount;
        db.ReserveTransactions.Add(new ReserveTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = type,
            Amount = -amount,
            Description = description,
            GameDay = gameDay,
        });
    }

    private static MineDetailResponse MapMineDetail(
        PlayerEntity player,
        MineEntity mine,
        List<InventoryItemEntity> inventory,
        DayAdvanceResponse? latestDayReport = null,
        string? birthdayMessage = null,
        IReadOnlyList<EventCompletionDto>? eventCompletions = null,
        string? currentJobSlug = null,
        string? currentJobTitle = null) =>
        new(
            mine.Id,
            mine.Name,
            mine.AsteroidSeed,
            mine.Status.ToString(),
            player.CurrentGameDay,
            player.Credits,
            player.ReserveBalance,
            mine.Zones.Select(z => new MineZoneDto(
                z.Id, z.X, z.Y, (OreTypeDto)z.OreType, z.Richness, z.DepletedPct, z.IsSalvageZone)).ToList(),
            mine.Workers.Select(w => new WorkerDto(w.Id, w.Name, w.Skill, w.Salary, w.AssignedZoneId)).ToList(),
            inventory.Select(i => new InventoryItemDto(
                i.ItemType,
                i.Category.ToString(),
                i.Quantity,
                i.Condition,
                i.BrokenQuantity,
                i.IsNew)).ToList(),
            FeatureFlags.Phase1,
            UtcGameClock.Today.ToString("yyyy-MM-dd"),
            UtcGameClock.NextDayBoundaryUtc,
            latestDayReport,
            birthdayMessage,
            eventCompletions is { Count: > 0 } ? eventCompletions : null,
            currentJobSlug,
            currentJobTitle);

    private static MarketTodayResponse MapMarket(
        DailyMarketSnapshot market,
        ActiveMarketBonusesDto? eventBonuses = null) =>
        new(
            market.GameDay,
            market.Prices.Select(p => new MarketPriceDto(
                (SupplyTypeDto)p.SupplyType, p.Price, p.ChangePct)).ToList(),
            market.Source,
            eventBonuses is { SaleBonusPercent: > 0 } or { TradeBonusPercent: > 0 }
                ? eventBonuses
                : null);
}
