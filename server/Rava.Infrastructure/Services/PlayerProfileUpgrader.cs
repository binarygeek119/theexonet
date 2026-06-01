using Microsoft.EntityFrameworkCore;
using Rava.Core.Constants;
using Rava.Core.Services;
using Rava.Core.Validation;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Entities;

namespace Rava.Infrastructure.Services;

public class PlayerProfileUpgrader(AppDbContext db)
{
    private const int BatchSize = 50;

    public bool ApplyProfileDefaults(PlayerEntity player)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(player.ProfileMood))
        {
            player.ProfileMood = PlayerProfileDefaults.Mood;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(player.ProfileTheme))
        {
            player.ProfileTheme = PlayerProfileDefaults.Theme;
            changed = true;
        }
        else
        {
            var normalizedTheme = ProfileValidator.NormalizeTheme(player.ProfileTheme);
            if (!string.Equals(player.ProfileTheme, normalizedTheme, StringComparison.Ordinal))
            {
                player.ProfileTheme = normalizedTheme;
                changed = true;
            }
        }

        if (player.ProfileAboutMe is null)
        {
            player.ProfileAboutMe = PlayerProfileDefaults.AboutMe;
            changed = true;
        }

        if (player.ProfileMusic is null)
        {
            player.ProfileMusic = PlayerProfileDefaults.Music;
            changed = true;
        }

        if (player.ProfileInterests is null)
        {
            player.ProfileInterests = PlayerProfileDefaults.Interests;
            changed = true;
        }

        if (player.ProfileDiscord is null)
        {
            player.ProfileDiscord = string.Empty;
            changed = true;
        }

        if (player.ProfileBluesky is null)
        {
            player.ProfileBluesky = string.Empty;
            changed = true;
        }

        if (player.ProfileTwitter is null)
        {
            player.ProfileTwitter = string.Empty;
            changed = true;
        }

        if (player.ProfileYoutube is null)
        {
            player.ProfileYoutube = string.Empty;
            changed = true;
        }

        if (player.ProfileFacebook is null)
        {
            player.ProfileFacebook = string.Empty;
            changed = true;
        }

        var normalizedPreset = ProfileAvatarPresets.Normalize(player.ProfileAvatarPreset);
        if (!string.Equals(player.ProfileAvatarPreset, normalizedPreset, StringComparison.Ordinal))
        {
            player.ProfileAvatarPreset = normalizedPreset;
            changed = true;
        }

        var normalizedGender = ProfileGender.Normalize(player.ProfileGender);
        if (!string.Equals(player.ProfileGender, normalizedGender, StringComparison.Ordinal))
        {
            player.ProfileGender = normalizedGender;
            changed = true;
        }

        var normalizedPreferred = ProfilePreferredPronouns.Normalize(player.ProfilePreferredPronouns);
        if (!ProfileGender.RequiresPreferredPronouns(player.ProfileGender))
        {
            normalizedPreferred = string.Empty;
        }

        if (!string.Equals(player.ProfilePreferredPronouns, normalizedPreferred, StringComparison.Ordinal))
        {
            player.ProfilePreferredPronouns = normalizedPreferred;
            changed = true;
        }

        if (player.LastProcessedUtcDate == default)
        {
            player.LastProcessedUtcDate = DateOnly.FromDateTime(
                player.CreatedAt.Kind == DateTimeKind.Utc
                    ? player.CreatedAt
                    : player.CreatedAt.ToUniversalTime());
            changed = true;
        }

        return changed;
    }

    public async Task<bool> AssignProfileNumberIfMissingAsync(PlayerEntity player, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(player.ProfileNumber))
        {
            return false;
        }

        player.ProfileNumber = await GenerateUniqueProfileNumberAsync(ct);
        return true;
    }

    public async Task EnsurePlayerUpgradedAsync(PlayerEntity player, CancellationToken ct)
    {
        var changed = ApplyProfileDefaults(player);
        if (await AssignProfileNumberIfMissingAsync(player, ct))
        {
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task UpgradeAllProfileDefaultsAsync(CancellationToken ct)
    {
        await ProcessPlayersInBatchesAsync(
            player => Task.FromResult(ApplyProfileDefaults(player)),
            saveWhenBatchDirty: true,
            ct);
    }

    public Task<string> CreateUniqueProfileNumberAsync(CancellationToken ct) =>
        GenerateUniqueProfileNumberAsync(ct);

    public Task AssignAllMissingProfileNumbersAsync(CancellationToken ct) =>
        ProcessPlayersInBatchesAsync(
            player => AssignProfileNumberIfMissingAsync(player, ct),
            saveWhenBatchDirty: true,
            ct);

    private async Task ProcessPlayersInBatchesAsync(
        Func<PlayerEntity, Task<bool>> upgradePlayer,
        bool saveWhenBatchDirty,
        CancellationToken ct)
    {
        var skip = 0;
        while (true)
        {
            var batch = await db.Players
                .OrderBy(p => p.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                return;
            }

            var batchChanged = false;
            foreach (var player in batch)
            {
                if (await upgradePlayer(player))
                {
                    batchChanged = true;
                }
            }

            if (saveWhenBatchDirty && batchChanged)
            {
                await db.SaveChangesAsync(ct);
            }

            skip += BatchSize;
        }
    }

    private async Task<string> GenerateUniqueProfileNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var candidate = ProfileNumberGenerator.Generate();
            if (!await db.Players.AnyAsync(p => p.ProfileNumber == candidate, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to assign a unique profile number.");
    }
}
