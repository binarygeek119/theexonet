using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;
using Theexonet.Infrastructure.Data;
using Theexonet.Infrastructure.Entities;

namespace Theexonet.Infrastructure.Services;

public class AdminTestingActionsService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    PlayerToStaffMessageService playerToStaffMessageService,
    PeerMessageService peerMessageService,
    PlayerBanService playerBanService,
    MessageModerationService messageModerationService,
    HateSpeechTermsProvider hateSpeechTermsProvider,
    ILogger<AdminTestingActionsService> logger)
{
    private const string FlaggedMessageFallbackTerm = "shit";

    public async Task<(AdminTestingActionResponse? Result, string? Error)> SendStaffMessageAsync(
        int dummyIndex,
        Guid adminPlayerId,
        string adminUsername,
        CancellationToken ct)
    {
        var (profile, error) = await PrepareDummyAsync(dummyIndex, adminPlayerId, ensureFriendship: false, ct);
        if (error is not null)
        {
            return (null, error);
        }

        var body =
            $"[TEST] Staff report from {profile!.Username}: drill rig malfunction reported on sector 7. Please advise.";
        var (message, sendError) = await playerToStaffMessageService.SendFromPlayerAsync(
            profile.PlayerId,
            adminUsername.Trim(),
            body,
            ct);

        if (sendError is not null)
        {
            return (null, sendError);
        }

        return (new AdminTestingActionResponse(
            $"Sent player → staff message as {profile.Username}. Check the Messages tab.",
            message!.Id,
            MessageLogChannels.PlayerToStaff), null);
    }

    public async Task<(AdminTestingActionResponse? Result, string? Error)> SendPeerMessageToAdminAsync(
        int dummyIndex,
        Guid adminPlayerId,
        string adminUsername,
        CancellationToken ct)
    {
        var (profile, error) = await PrepareDummyAsync(dummyIndex, adminPlayerId, ensureFriendship: true, ct);
        if (error is not null)
        {
            return (null, error);
        }

        var body =
            $"[TEST] Hey {adminUsername}, this is a test friend message from {profile!.Username}.";
        var (message, sendError) = await peerMessageService.SendMessageAsync(
            profile.PlayerId,
            adminPlayerId,
            body,
            ct);

        if (sendError is not null)
        {
            return (null, sendError);
        }

        return (new AdminTestingActionResponse(
            $"Sent peer message from {profile.Username} to you. Check in-game Messages or Message Log.",
            message!.Id,
            MessageLogChannels.Peer), null);
    }

    public async Task<(AdminTestingActionResponse? Result, string? Error)> SendFlaggedMessageAsync(
        int dummyIndex,
        Guid adminPlayerId,
        string adminUsername,
        CancellationToken ct)
    {
        var flaggedTerm = ResolveFlaggedTestTerm();
        if (flaggedTerm is null)
        {
            return (null, "Hate speech scanner has no terms loaded. Enable HateSpeech:Enabled and add terms to the CSV files.");
        }

        var (profile, error) = await PrepareDummyAsync(dummyIndex, adminPlayerId, ensureFriendship: true, ct);
        if (error is not null)
        {
            return (null, error);
        }

        var body =
            $"[TEST] Moderation scan test from {profile!.Username}: this message contains {flaggedTerm}.";
        var (message, sendError) = await peerMessageService.SendMessageAsync(
            profile.PlayerId,
            adminPlayerId,
            body,
            ct);

        if (sendError is not null)
        {
            return (null, sendError);
        }

        var flagged = await db.FlaggedMessages.AsNoTracking()
            .AnyAsync(f => f.SourceMessageId == message!.Id, ct);

        if (!flagged)
        {
            return (null, "Message was sent but was not flagged. Check that HateSpeech:Enabled is true in appsettings.");
        }

        return (new AdminTestingActionResponse(
            $"Sent flagged peer message as {profile.Username}. Check the Flagged messages tab.",
            message!.Id,
            MessageLogChannels.Peer), null);
    }

    public async Task<(AdminTestingActionResponse? Result, string? Error)> SubmitBanAppealAsync(
        int dummyIndex,
        Guid adminPlayerId,
        string adminUsername,
        CancellationToken ct)
    {
        var (profile, error) = await PrepareDummyAsync(dummyIndex, adminPlayerId, ensureFriendship: false, ct);
        if (error is not null)
        {
            return (null, error);
        }

        var playerId = profile!.PlayerId;
        var hasPending = await db.BanAppeals.AnyAsync(
            a => a.PlayerId == playerId && a.Status == BanAppealStatuses.Pending,
            ct);
        if (hasPending)
        {
            return (null, $"{profile.Username} already has a pending ban appeal.");
        }

        var activeBan = await playerBanService.GetActiveBanAsync(playerId, ct);
        if (activeBan is null)
        {
            var (ban, banError) = await playerBanService.SetBanAsync(
                playerId,
                BanLevels.FifteenMinutes,
                adminUsername.Trim(),
                "[TEST] Temporary ban for appeal workflow testing.",
                ct);

            if (banError is not null)
            {
                return (null, banError);
            }

            activeBan = ban;
        }

        var appealMessage =
            $"[TEST] Ban appeal from {profile.Username}: I believe this ban was a mistake. Please review.";
        var appeal = new BanAppealEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            BanId = activeBan!.Id,
            Message = appealMessage,
            Status = BanAppealStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.BanAppeals.Add(appeal);
        await db.SaveChangesAsync(ct);

        MessageAuditLogger.LogSent(
            logger,
            MessageLogChannels.BanAppeal,
            profile.Username,
            "admins",
            appeal.Id,
            appealMessage);

        await messageModerationService.ScanAndFlagIfNeededAsync(
            MessageLogChannels.BanAppeal,
            appeal.Id,
            playerId,
            profile.Username,
            "admins",
            appealMessage,
            ct);

        return (new AdminTestingActionResponse(
            $"Submitted ban appeal as {profile.Username}. Check the Ban appeals tab.",
            appeal.Id,
            MessageLogChannels.BanAppeal), null);
    }

    private async Task<(TestingDummyFriendsProfile? Profile, string? Error)> PrepareDummyAsync(
        int dummyIndex,
        Guid adminPlayerId,
        bool ensureFriendship,
        CancellationToken ct)
    {
        if (!TestingDummyFriendsCatalog.IsValidIndex(dummyIndex))
        {
            return (null, "Select a testing player between 0 and 11.");
        }

        var profile = TestingDummyFriendsCatalog.Get(dummyIndex);
        await EnsureDummyPlayerAsync(profile, ct);

        if (ensureFriendship)
        {
            await EnsureDummyFriendshipAsync(dummyIndex, adminPlayerId, profile.PlayerId, ct);
        }

        return (profile, null);
    }

    private async Task EnsureDummyPlayerAsync(TestingDummyFriendsProfile profile, CancellationToken ct)
    {
        var exists = await db.Players.AsNoTracking().AnyAsync(p => p.Id == profile.PlayerId, ct);
        if (exists)
        {
            return;
        }

        var player = new PlayerEntity
        {
            Id = profile.PlayerId,
            Username = profile.Username,
            Email = $"{profile.Username}@testing.theexonet",
            PasswordHash = passwordHasher.Hash("TestingDummy!"),
            Credits = 0,
            CurrentGameDay = 1,
            LastProcessedUtcDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ProfileNumber = profile.ProfileNumber,
            ProfileMood = profile.Mood,
            ProfileAboutMe = profile.AboutMe,
            ProfileInterests = profile.Interests,
            ProfileMusic = profile.Music,
            CreatedAt = DateTime.UtcNow
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureDummyFriendshipAsync(
        int dummyIndex,
        Guid adminPlayerId,
        Guid dummyPlayerId,
        CancellationToken ct)
    {
        var alreadyFriends = await db.Friendships.AsNoTracking().AnyAsync(
            f => f.Status == FriendshipStatuses.Accepted
                 && ((f.PlayerId == adminPlayerId && f.FriendId == dummyPlayerId)
                     || (f.PlayerId == dummyPlayerId && f.FriendId == adminPlayerId)),
            ct);

        if (alreadyFriends)
        {
            return;
        }

        db.Friendships.Add(new FriendshipEntity
        {
            Id = TestingDummyFriendsCatalog.DummyFriendshipId(dummyIndex),
            PlayerId = adminPlayerId,
            FriendId = dummyPlayerId,
            Status = FriendshipStatuses.Accepted,
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    private string? ResolveFlaggedTestTerm()
    {
        var terms = hateSpeechTermsProvider.GetTerms();
        foreach (var term in terms)
        {
            if (!string.IsNullOrWhiteSpace(term))
            {
                return term.Trim();
            }
        }

        return FlaggedMessageFallbackTerm;
    }
}
