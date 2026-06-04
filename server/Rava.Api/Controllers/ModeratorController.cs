using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Rava.Api.Services;
using Rava.Core.Configuration;
using Rava.Core.Constants;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/moderator")]
[Authorize(Policy = "Moderator")]
public class ModeratorController(
    AdminService adminService,
    BanAppealService banAppealService,
    MessageModerationService messageModerationService,
    MessageLogService messageLogService,
    IEmailService emailService,
    IOptions<EmailOptions> emailOptions,
    ILogger<ModeratorController> logger) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardResponse>> Dashboard(CancellationToken ct)
    {
        return Ok(await adminService.GetDashboardAsync(ct));
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

        var profile = await adminService.GetPlayerProfileAsync(playerId, ct);
        var activeCount = profile?.WarningCount ?? 0;

        return Ok(new PlayerWarningResponse(
            warning!,
            $"Warning issued ({activeCount}/{ModerationWarningLimits.MaxWarningsBeforeBan} active). Expires in {ModerationWarningLimits.WarningDurationDays} days. Player was not notified."));
    }

    [HttpGet("ban-appeals")]
    public async Task<ActionResult<BanAppealsResponse>> BanAppeals(CancellationToken ct) =>
        Ok(new BanAppealsResponse(await banAppealService.GetPendingAppealsAsync(ct)));

    [HttpGet("bans")]
    public async Task<ActionResult<AdminBansResponse>> Bans(
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int limit = 100,
        CancellationToken ct = default) =>
        Ok(await adminService.GetBansAsync(search, activeOnly, limit, ct));

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
}

[ApiController]
[Route("api/moderator")]
public class ModeratorAccessController(
    IOptions<ModeratorOptions> moderatorOptions,
    IOptions<AdminOptions> adminOptions) : ControllerBase
{
    [Authorize]
    [HttpGet("access")]
    public ActionResult<ModeratorMeResponse> Access()
    {
        var username = User.GetUsername() ?? string.Empty;
        var isModerator = IsStaffUsername(username, moderatorOptions.Value.Usernames);
        var isAdmin = IsStaffUsername(username, adminOptions.Value.Usernames);
        return Ok(new ModeratorMeResponse(username, isModerator || isAdmin, isAdmin));
    }

    private static bool IsStaffUsername(string username, string[] allowed) =>
        !string.IsNullOrWhiteSpace(username)
        && (allowed ?? []).Any(name => string.Equals(name, username, StringComparison.OrdinalIgnoreCase));
}
