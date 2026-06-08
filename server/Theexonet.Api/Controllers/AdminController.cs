using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Theexonet.Api.Services;
using Theexonet.Api.Services.AiImageQueue;
using Theexonet.Api.Services.Foreverfall;
using Theexonet.Infrastructure.Services;
using Theexonet.Api.Services.LunarWeather;
using Theexonet.Api.Services.OffworldNews;
using Theexonet.Api.Services.VoidCorp;
using Theexonet.Api.Services.TestingDummyFriends;
using Theexonet.Core.Configuration;
using Theexonet.Core.Constants;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Core.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Admin")]
public class AdminController(
    AdminService adminService,
    BanAppealService banAppealService,
    MessageLogService messageLogService,
    MessageModerationService messageModerationService,
    GameCreditsConfigService gameCreditsConfigService,
    SpecialEventService specialEventService,
    OffworldNewsService offworldNewsService,
    IServiceScopeFactory scopeFactory,
    OffworldNewsReporterRosterAdminService offworldNewsReporterRoster,
    OffworldNewsAdminSettingsStore offworldNewsAdminSettings,
    LunarWeatherService lunarWeatherService,
    LunarWeatherAdminSettingsStore lunarWeatherAdminSettings,
    ForeverfallPenitentiaryService foreverfallPenitentiaryService,
    ForeverfallAdminSettingsStore foreverfallAdminSettings,
    VoidCorpAdminService voidCorpAdminService,
    IEmailService emailService,
    IOptions<EmailOptions> emailOptions,
    IOptions<AdminOptions> adminOptions,
    ILogger<AdminController> logger) : ControllerBase
{
    [HttpGet("me")]
    public ActionResult<AdminMeResponse> Me()
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = IsAdminUsername(username);
        if (!isAdmin)
        {
            return Forbid();
        }

        return Ok(new AdminMeResponse(username, true));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardResponse>> Dashboard(CancellationToken ct)
    {
        return Ok(await adminService.GetDashboardAsync(ct));
    }

    [HttpPost("offworld-news/regenerate-edition")]
    public async Task<ActionResult<AdminOffworldNewsRegenerateResponse>> RegenerateOffworldNewsEdition(CancellationToken ct)
    {
        var (edition, error) = await offworldNewsService.RegenerateTodayEditionAsync(ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(OffworldNewsService.ToRegenerateResponse(
            edition!,
            "Today's Offworld News stories regenerated; story images queued for generation."));
    }

    [HttpPost("offworld-news/regenerate-images")]
    public async Task<ActionResult<AdminOffworldNewsRegenerateResponse>> RegenerateOffworldNewsImages(CancellationToken ct)
    {
        var (edition, error) = await offworldNewsService.RegenerateTodayImagesAsync(ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(OffworldNewsService.ToRegenerateResponse(
            edition!,
            "Today's Offworld News AI images queued for regeneration. Archive editions were not changed."));
    }

    [HttpPost("offworld-news/regenerate-reporter-portraits")]
    public async Task<ActionResult<AdminAiImageQueueStatusDto>> RegenerateOffworldNewsReporterPortraits(
        CancellationToken ct)
    {
        var (_, error) = await offworldNewsService.RegenerateReporterPortraitsAsync(ct: ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Accepted(await GetAiImageQueueStatusInternalAsync("onn_reporter", ct));
    }

    [HttpGet("offworld-news/reporter-portraits-job")]
    public async Task<ActionResult<AdminAiImageQueueStatusDto>> GetOffworldNewsReporterPortraitJob(
        CancellationToken ct) =>
        Ok(await GetAiImageQueueStatusInternalAsync("onn_reporter", ct));

    [HttpGet("offworld-news/reporters")]
    public ActionResult<AdminOffworldNewsReportersPageDto> GetOffworldNewsReporters() =>
        Ok(offworldNewsReporterRoster.GetPage());

    [HttpPost("offworld-news/reporters")]
    public async Task<ActionResult<AdminOffworldNewsReporterRowDto>> CreateOffworldNewsReporter(
        AdminCreateOffworldNewsReporterRequest request,
        CancellationToken ct)
    {
        var (reporter, error) = await offworldNewsReporterRoster.AddReporterAsync(request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(reporter);
    }

    [HttpPut("offworld-news/reporters/{slug}")]
    public async Task<ActionResult<AdminOffworldNewsReporterRowDto>> UpdateOffworldNewsReporter(
        string slug,
        AdminUpdateOffworldNewsReporterRequest request,
        CancellationToken ct)
    {
        var (reporter, error) = await offworldNewsReporterRoster.UpdateReporterAsync(slug, request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(reporter);
    }

    [HttpPost("offworld-news/reporters/{slug}/regenerate-portraits")]
    public async Task<ActionResult<AdminAiImageQueueStatusDto>> RegenerateOffworldNewsReporterPortraits(
        string slug,
        [FromQuery] string? assets = "both",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)
            || slug.Equals("undefined", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Reporter slug is required." });
        }

        if (!ReporterPortraitAssetKindParser.TryParse(assets, out var assetKind, out var parseError))
        {
            return BadRequest(new { message = parseError });
        }

        var (_, error) = await offworldNewsService.RegenerateReporterPortraitsAsync(
            [slug.Trim()],
            assetKind,
            ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Accepted(await GetAiImageQueueStatusInternalAsync("onn_reporter", ct));
    }

    [HttpGet("offworld-news/settings")]
    public ActionResult<AdminOffworldNewsSettingsDto> GetOffworldNewsSettings() =>
        Ok(offworldNewsAdminSettings.GetSettings());

    [HttpPut("offworld-news/settings")]
    public ActionResult<AdminOffworldNewsSettingsDto> UpdateOffworldNewsSettings(
        AdminUpdateOffworldNewsSettingsRequest request)
    {
        var (settings, error) = offworldNewsReporterRoster.SaveSettings(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(settings);
    }

    [HttpGet("lunar-weather/settings")]
    public ActionResult<AdminLunarWeatherSettingsDto> GetLunarWeatherSettings() =>
        Ok(lunarWeatherAdminSettings.GetSettings());

    [HttpPut("lunar-weather/settings")]
    public ActionResult<AdminLunarWeatherSettingsDto> UpdateLunarWeatherSettings(
        AdminUpdateLunarWeatherSettingsRequest request)
    {
        var (settings, error) = lunarWeatherAdminSettings.Save(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(settings);
    }

    [HttpPost("lunar-weather/regenerate-bulletin")]
    public async Task<ActionResult<AdminLunarWeatherRegenerateResponse>> RegenerateLunarWeatherBulletin(
        CancellationToken ct)
    {
        var (bulletin, error) = await lunarWeatherService.RegenerateBulletinAndWaitAsync(ct);
        if (error is not null || bulletin is null)
        {
            return BadRequest(new { message = error ?? "Bulletin regeneration failed." });
        }

        return Ok(new AdminLunarWeatherRegenerateResponse(
            "Today's Lunar Weather bulletin regenerated.",
            bulletin.BulletinDate,
            bulletin.Source,
            bulletin.OperationalCount,
            bulletin.OutageCount));
    }

    [HttpGet("foreverfall/settings")]
    public ActionResult<AdminForeverfallSettingsDto> GetForeverfallSettings() =>
        Ok(foreverfallAdminSettings.GetSettings(foreverfallPenitentiaryService.GetPortraitPoolCount()));

    [HttpPut("foreverfall/settings")]
    public ActionResult<AdminForeverfallSettingsDto> UpdateForeverfallSettings(
        AdminUpdateForeverfallSettingsRequest request)
    {
        var (settings, error) = foreverfallAdminSettings.Save(
            request,
            foreverfallPenitentiaryService.GetPortraitPoolCount());
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(settings);
    }

    [HttpGet("foreverfall/status")]
    public ActionResult<AdminForeverfallStatusDto> GetForeverfallStatus() =>
        Ok(foreverfallPenitentiaryService.GetStatus());

    [HttpPost("foreverfall/regenerate-intake")]
    public async Task<ActionResult<AdminForeverfallRegenerateResponse>> RegenerateForeverfallIntake(
        CancellationToken ct)
    {
        var (ok, error, portraitsQueued) = await foreverfallPenitentiaryService.RegenerateIntakeAndWaitAsync(ct);
        if (!ok || error is not null)
        {
            return BadRequest(new { message = error ?? "Intake regeneration failed." });
        }

        var today = UtcGameClock.Today;
        var roster = await foreverfallPenitentiaryService.GetRosterAsync(today, ct);
        return Ok(new AdminForeverfallRegenerateResponse(
            "Today's Foreverfall Penitentiary intake regenerated.",
            roster.IntakeDate,
            roster.Source,
            roster.IntakeCount,
            roster.MaleCount,
            roster.FemaleCount,
            portraitsQueued));
    }

    [HttpPost("foreverfall/regenerate-portraits")]
    public async Task<ActionResult<AdminForeverfallRegenerateResponse>> RegenerateForeverfallPortraits(
        CancellationToken ct)
    {
        var (ok, error, portraitsQueued) = await foreverfallPenitentiaryService.RegenerateTodayPortraitsAsync(ct);
        if (!ok || error is not null)
        {
            return BadRequest(new { message = error ?? "Portrait regeneration failed." });
        }

        var today = UtcGameClock.Today;
        var roster = await foreverfallPenitentiaryService.GetRosterAsync(today, ct);
        return Ok(new AdminForeverfallRegenerateResponse(
            "Today's Foreverfall Penitentiary AI portraits queued for regeneration. Inmate dossiers and archive rosters were not changed.",
            roster.IntakeDate,
            roster.Source,
            roster.IntakeCount,
            roster.MaleCount,
            roster.FemaleCount,
            portraitsQueued));
    }

    [HttpGet("foreverfall/portrait-job")]
    public async Task<ActionResult<AdminAiImageQueueStatusDto>> GetForeverfallPortraitJob(
        CancellationToken ct) =>
        Ok(await GetAiImageQueueStatusInternalAsync(AiImageJobKinds.ForeverfallPortrait, ct));

    [HttpGet("ai-image-queue")]
    public async Task<ActionResult<AdminAiImageQueueStatusDto>> GetAiImageQueueStatus(
        [FromQuery] string? kind,
        CancellationToken ct) =>
        Ok(await GetAiImageQueueStatusInternalAsync(kind, ct));

    private async Task<AdminAiImageQueueStatusDto> GetAiImageQueueStatusInternalAsync(
        string? kind,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<AiImageQueueService>();
        return await queue.GetStatusAsync(kind, ct);
    }

    [HttpGet("voidcorp/status")]
    public ActionResult<AdminVoidCorpStatusDto> GetVoidCorpStatus() =>
        Ok(voidCorpAdminService.GetStatus());

    [HttpPost("voidcorp/generate-missing-images")]
    public async Task<ActionResult<AdminVoidCorpGenerateImagesResponse>> GenerateVoidCorpMissingImages(
        CancellationToken ct)
    {
        var (response, error) = await voidCorpAdminService.GenerateMissingImagesAsync(ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(response);
    }

    [HttpPost("voidcorp/regenerate-images")]
    public async Task<ActionResult<AdminVoidCorpGenerateImagesResponse>> RegenerateVoidCorpImages(
        CancellationToken ct)
    {
        var (response, error) = await voidCorpAdminService.RegenerateImagesAsync(ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(response);
    }

    [HttpGet("players")]
    public async Task<ActionResult<AdminPlayersResponse>> Players(
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        return Ok(await adminService.GetPlayersAsync(search, limit, ct));
    }

    [HttpGet("players/{playerId:guid}/profile")]
    public async Task<ActionResult<AdminPlayerProfileResponse>> PlayerProfile(Guid playerId, CancellationToken ct)
    {
        var profile = await adminService.GetPlayerProfileAsync(playerId, ct);
        if (profile is null)
        {
            return NotFound(new { message = "Player not found." });
        }

        return Ok(profile);
    }

    [HttpPut("players/{playerId:guid}/credits")]
    public async Task<ActionResult<AdminPlayerSummary>> SetCredits(
        Guid playerId,
        AdminSetCreditsRequest request,
        CancellationToken ct)
    {
        var (player, error) = await adminService.SetCreditsAsync(playerId, request.Credits, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(player);
    }

    [HttpGet("game-credits-config")]
    public ActionResult<GameCreditsConfigResponse> GetGameCreditsConfig() =>
        Ok(gameCreditsConfigService.GetConfig());

    [HttpPut("game-credits-config")]
    public async Task<ActionResult<UpdateGameCreditsConfigResponse>> UpdateGameCreditsConfig(
        UpdateGameCreditsConfigRequest request,
        CancellationToken ct)
    {
        var (credits, error) = await gameCreditsConfigService.SaveAsync(
            request.SignUp,
            request.BirthdayBonus,
            request.CompanyNameReclaimFee,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new UpdateGameCreditsConfigResponse(
            credits!,
            "Credits configuration saved. New sign-ups, birthday bonuses, and reclaim fees use these values immediately."));
    }

    [HttpGet("special-events")]
    public async Task<ActionResult<SpecialEventsListResponse>> ListSpecialEvents(CancellationToken ct) =>
        Ok(await specialEventService.ListAsync(ct));

    [HttpPost("special-events")]
    public async Task<ActionResult<SpecialEventDto>> CreateSpecialEvent(
        SaveSpecialEventRequest request,
        CancellationToken ct)
    {
        var (evt, error) = await specialEventService.CreateAsync(request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(evt);
    }

    [HttpPut("special-events/{eventId:guid}")]
    public async Task<ActionResult<SpecialEventDto>> UpdateSpecialEvent(
        Guid eventId,
        SaveSpecialEventRequest request,
        CancellationToken ct)
    {
        var (evt, error) = await specialEventService.UpdateAsync(eventId, request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(evt);
    }

    [HttpDelete("special-events/{eventId:guid}")]
    public async Task<ActionResult<MessageResponse>> DeleteSpecialEvent(Guid eventId, CancellationToken ct)
    {
        var error = await specialEventService.DeleteAsync(eventId, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new MessageResponse("Event deleted."));
    }

    [HttpPost("players/{playerId:guid}/flag")]
    public async Task<ActionResult<ProfileFlagResponse>> FlagProfile(
        Guid playerId,
        ProfileFlagRequest request,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (flag, player, error) = await adminService.FlagPlayerProfileAsync(
            playerId,
            staffUsername,
            request.Comment,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var baseUrl = emailOptions.Value.AppBaseUrl.TrimEnd('/');
        var playerEmail = player!.Email;
        try
        {
            await emailService.SendProfileFlagAsync(
                playerEmail,
                player.Username,
                flag!.Comment,
                baseUrl,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile flag email failed for {Email}", playerEmail);
            return StatusCode(503, new
            {
                message = "Profile was flagged, but the notification email could not be sent. Verify SMTP settings and restart the API."
            });
        }

        return Ok(new ProfileFlagResponse(flag, "Profile flagged and player notified by email."));
    }

    [HttpGet("ban-levels")]
    public async Task<ActionResult<IReadOnlyList<BanLevelOptionDto>>> BanLevels(CancellationToken ct) =>
        Ok(await adminService.GetBanLevelOptions());

    [HttpGet("ban-reasons")]
    public async Task<ActionResult<BanReasonPresetsResponse>> BanReasons(CancellationToken ct) =>
        Ok(await adminService.GetBanReasonPresets());

    [HttpPost("players/{playerId:guid}/ban")]
    public async Task<ActionResult<PlayerBanActionResponse>> BanPlayer(
        Guid playerId,
        SetPlayerBanRequest request,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (ban, error) = await adminService.SetPlayerBanAsync(
            playerId,
            request.BanLevel,
            staffUsername,
            request.Reason,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new PlayerBanActionResponse(
            ban,
            $"Player banned for {ban!.BanLevelLabel}."));
    }

    [HttpPost("players/{playerId:guid}/unban")]
    public async Task<ActionResult<PlayerBanActionResponse>> UnbanPlayer(Guid playerId, CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (ban, error) = await adminService.LiftPlayerBanAsync(playerId, staffUsername, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new PlayerBanActionResponse(ban, "Player ban lifted."));
    }

    [HttpPost("players/{playerId:guid}/warn")]
    public async Task<ActionResult<PlayerWarningResponse>> WarnPlayer(
        Guid playerId,
        IssuePlayerWarningRequest request,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (warning, error) = await adminService.IssuePlayerWarningAsync(
            playerId,
            staffUsername,
            request.Reason,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var activeCount = warning!.IsActive ? 1 : 0;
        var profile = await adminService.GetPlayerProfileAsync(playerId, ct);
        activeCount = profile?.WarningCount ?? activeCount;

        return Ok(new PlayerWarningResponse(
            warning!,
            $"Warning issued ({activeCount}/{ModerationWarningLimits.MaxWarningsBeforeBan} active). Expires in {ModerationWarningLimits.WarningDurationDays} days. Player was not notified."));
    }

    [HttpGet("bans")]
    public async Task<ActionResult<AdminBansResponse>> Bans(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int limit = 100,
        CancellationToken ct = default) =>
        Ok(await adminService.GetBansAsync(search, activeOnly, limit, ct));

    [HttpGet("ban-appeals")]
    public async Task<ActionResult<BanAppealsResponse>> BanAppeals(CancellationToken ct) =>
        Ok(new BanAppealsResponse(await banAppealService.GetPendingAppealsAsync(ct)));

    [HttpGet("message-log")]
    public async Task<ActionResult<MessageLogResponse>> MessageLog(
        [FromQuery] string? search,
        [FromQuery] string? channel,
        [FromQuery] int limit = 100,
        CancellationToken ct = default) =>
        Ok(await messageLogService.GetLogAsync(search, channel, limit, ct));

    [HttpPost("ban-appeals/{appealId:guid}/dismiss")]
    public async Task<ActionResult<BanAppealDto>> DismissBanAppeal(Guid appealId, CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (appeal, error) = await banAppealService.DismissAppealAsync(appealId, staffUsername, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(appeal);
    }

    [HttpGet("flagged-messages")]
    public async Task<ActionResult<FlaggedMessagesResponse>> FlaggedMessages(CancellationToken ct) =>
        Ok(new FlaggedMessagesResponse(await messageModerationService.GetPendingReviewsAsync(ct)));

    [HttpGet("flagged-messages/pending-count")]
    public async Task<ActionResult<FlaggedMessagePendingCountResponse>> FlaggedMessagePendingCount(CancellationToken ct) =>
        Ok(new FlaggedMessagePendingCountResponse(await messageModerationService.GetPendingCountAsync(ct)));

    [HttpPost("flagged-messages/{flaggedMessageId:guid}/dismiss")]
    public async Task<ActionResult<FlaggedMessageReviewDto>> DismissFlaggedMessage(
        Guid flaggedMessageId,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (review, error) = await messageModerationService.DismissAsync(flaggedMessageId, staffUsername, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(review);
    }

    [HttpPost("flagged-messages/{flaggedMessageId:guid}/warn")]
    public async Task<ActionResult<FlaggedMessageWarningResponse>> WarnFromFlaggedMessage(
        Guid flaggedMessageId,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (warning, review, error) = await messageModerationService.IssueWarningAsync(
            flaggedMessageId,
            staffUsername,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new FlaggedMessageWarningResponse(
            warning!,
            review!,
            $"Warning issued ({review!.PlayerWarningCount}/{ModerationWarningLimits.MaxWarningsBeforeBan} active). Expires in {ModerationWarningLimits.WarningDurationDays} days. Player was not notified."));
    }

    [HttpPost("flagged-messages/{flaggedMessageId:guid}/ban")]
    public async Task<ActionResult<FlaggedMessageBanResponse>> BanFromFlaggedMessage(
        Guid flaggedMessageId,
        SetPlayerBanRequest request,
        CancellationToken ct)
    {
        var staffUsername = User.GetUsername() ?? string.Empty;
        var (ban, review, error) = await messageModerationService.IssueBanAsync(
            flaggedMessageId,
            staffUsername,
            request.BanLevel,
            request.Reason,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new FlaggedMessageBanResponse(
            ban!,
            review!,
            $"Player banned for {ban!.BanLevelLabel}."));
    }

    private bool IsAdminUsername(string username) =>
        adminOptions.Value.IsAdminUsername(username);
}

[ApiController]
[Route("api/admin")]
public class AdminAccessController(
    IOptions<AdminOptions> adminOptions,
    AdminService adminService,
    AdminTestingActionsService adminTestingActionsService,
    TheexonetHostingPaths hostingPaths,
    TestingDummyFriendsAssetService testingDummyFriendsAssetService) : ControllerBase
{
    [Authorize]
    [HttpGet("access")]
    public async Task<ActionResult<AdminMeResponse>> Access(CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = adminOptions.Value.IsAdminUsername(username);
        var response = await adminService.GetAdminAccessAsync(User.GetPlayerId(), username, isAdmin, ct);
        return Ok(response);
    }

    [Authorize]
    [HttpPut("testing-mode")]
    public async Task<ActionResult<AdminTestingModeResponse>> SetTestingMode(
        [FromBody] AdminTestingModeRequest request,
        CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = adminOptions.Value.IsAdminUsername(username);
        var (result, error) = await adminService.SetAdminTestingModeAsync(
            User.GetPlayerId(),
            isAdmin,
            request.Enabled,
            ct);
        if (error is not null)
        {
            return isAdmin ? BadRequest(new { message = error }) : Forbid();
        }

        if (request.Enabled)
        {
            testingDummyFriendsAssetService.TryStartEnsureMissing();
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("testing-dummy-assets/ensure")]
    public async Task<ActionResult<TestingDummyAssetsEnsureResponse>> EnsureTestingDummyAssets(CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = adminOptions.Value.IsAdminUsername(username);
        if (!isAdmin)
        {
            return Forbid();
        }

        var access = await adminService.GetAdminAccessAsync(User.GetPlayerId(), username, true, ct);
        if (!access.TestingModeEnabled)
        {
            return BadRequest(new { message = "Turn on testing mode in the admin portal first." });
        }

        var missing = TestingDummyFriendsAssetService.CountMissingAssets(hostingPaths.TestingDummyFriendsAssetsRoot);
        if (missing == 0)
        {
            return Ok(new TestingDummyAssetsEnsureResponse(
                false,
                testingDummyFriendsAssetService.IsRunning,
                0,
                "All testing player profile assets are already present."));
        }

        if (testingDummyFriendsAssetService.IsRunning)
        {
            return Ok(new TestingDummyAssetsEnsureResponse(
                false,
                true,
                missing,
                "Testing player asset generation is already running."));
        }

        if (!testingDummyFriendsAssetService.TryStartEnsureMissing())
        {
            return BadRequest(new { message = "AI image generation is not configured on this server." });
        }

        return Ok(new TestingDummyAssetsEnsureResponse(
            true,
            false,
            missing,
            "Generating missing testing player avatars, banners, and company logos."));
    }

    [Authorize]
    [HttpPost("testing-actions/staff-message")]
    public async Task<ActionResult<AdminTestingActionResponse>> TestingActionStaffMessage(
        [FromBody] AdminTestingDummyActionRequest request,
        CancellationToken ct) =>
        await RunTestingActionAsync(
            request.DummyIndex,
            (playerId, username) => adminTestingActionsService.SendStaffMessageAsync(
                request.DummyIndex,
                playerId,
                username,
                ct),
            ct);

    [Authorize]
    [HttpPost("testing-actions/peer-message")]
    public async Task<ActionResult<AdminTestingActionResponse>> TestingActionPeerMessage(
        [FromBody] AdminTestingDummyActionRequest request,
        CancellationToken ct) =>
        await RunTestingActionAsync(
            request.DummyIndex,
            (playerId, username) => adminTestingActionsService.SendPeerMessageToAdminAsync(
                request.DummyIndex,
                playerId,
                username,
                ct),
            ct);

    [Authorize]
    [HttpPost("testing-actions/flagged-message")]
    public async Task<ActionResult<AdminTestingActionResponse>> TestingActionFlaggedMessage(
        [FromBody] AdminTestingDummyActionRequest request,
        CancellationToken ct) =>
        await RunTestingActionAsync(
            request.DummyIndex,
            (playerId, username) => adminTestingActionsService.SendFlaggedMessageAsync(
                request.DummyIndex,
                playerId,
                username,
                ct),
            ct);

    [Authorize]
    [HttpPost("testing-actions/ban-appeal")]
    public async Task<ActionResult<AdminTestingActionResponse>> TestingActionBanAppeal(
        [FromBody] AdminTestingDummyActionRequest request,
        CancellationToken ct) =>
        await RunTestingActionAsync(
            request.DummyIndex,
            (playerId, username) => adminTestingActionsService.SubmitBanAppealAsync(
                request.DummyIndex,
                playerId,
                username,
                ct),
            ct);

    private async Task<ActionResult<AdminTestingActionResponse>> RunTestingActionAsync(
        int dummyIndex,
        Func<Guid, string, Task<(AdminTestingActionResponse? Result, string? Error)>> action,
        CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = adminOptions.Value.IsAdminUsername(username);
        if (!isAdmin)
        {
            return Forbid();
        }

        var access = await adminService.GetAdminAccessAsync(User.GetPlayerId(), username, true, ct);
        if (!access.TestingModeEnabled)
        {
            return BadRequest(new { message = "Turn on testing mode in the admin portal first." });
        }

        var (result, error) = await action(User.GetPlayerId(), username);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }
}
