using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Rava.Api.Services;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Core.Services;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

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
    OffworldNewsReporterPortraitJobService offworldNewsReporterPortraitJob,
    OffworldNewsReporterRosterAdminService offworldNewsReporterRoster,
    OffworldNewsAdminSettingsStore offworldNewsAdminSettings,
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
            "Today's Offworld News stories and images were regenerated."));
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
            "Today's Offworld News images were regenerated."));
    }

    [HttpPost("offworld-news/regenerate-reporter-portraits")]
    public ActionResult<AdminOffworldNewsReporterPortraitJobDto> RegenerateOffworldNewsReporterPortraits()
    {
        var (started, error) = offworldNewsReporterPortraitJob.TryStart(slugs: null);
        if (!started)
        {
            return BadRequest(new { message = error });
        }

        return Accepted(offworldNewsReporterPortraitJob.GetStatus());
    }

    [HttpGet("offworld-news/reporter-portraits-job")]
    public ActionResult<AdminOffworldNewsReporterPortraitJobDto> GetOffworldNewsReporterPortraitJob() =>
        Ok(offworldNewsReporterPortraitJob.GetStatus());

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
    public ActionResult<AdminOffworldNewsReporterPortraitJobDto> RegenerateOffworldNewsReporterPortraits(
        string slug,
        [FromQuery] string? assets = "both")
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

        var (started, error) = offworldNewsReporterPortraitJob.TryStart([slug.Trim()], assetKind);
        if (!started)
        {
            return BadRequest(new { message = error });
        }

        return Accepted(offworldNewsReporterPortraitJob.GetStatus());
    }

    [HttpGet("offworld-news/settings")]
    public ActionResult<AdminOffworldNewsSettingsDto> GetOffworldNewsSettings() =>
        Ok(new AdminOffworldNewsSettingsDto(
            offworldNewsAdminSettings.ReporterPoolSize,
            OffworldNewsReporterCatalog.All.Count,
            offworldNewsAdminSettings.ActivePoolCount()));

    [HttpPut("offworld-news/settings")]
    public ActionResult<AdminOffworldNewsSettingsDto> UpdateOffworldNewsSettings(
        AdminUpdateOffworldNewsSettingsRequest request)
    {
        var (settings, error) = offworldNewsReporterRoster.SaveSettings(request.ReporterPoolSize);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(settings);
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
public class AdminAccessController(IOptions<AdminOptions> adminOptions) : ControllerBase
{
    [Authorize]
    [HttpGet("access")]
    public ActionResult<AdminMeResponse> Access()
    {
        var username = User.GetUsername() ?? string.Empty;
        var isAdmin = adminOptions.Value.IsAdminUsername(username);
        return Ok(new AdminMeResponse(username, isAdmin));
    }
}
