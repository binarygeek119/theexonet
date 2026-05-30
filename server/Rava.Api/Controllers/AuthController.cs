using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rava.Api.Services;
using Rava.Core.Configuration;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Infrastructure.Data;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    PlayerGameService gameService,
    PlayerBanService playerBanService,
    BanAppealService banAppealService,
    SpecialEventService specialEventService,
    AppDbContext db,
    ITokenService tokenService,
    IEmailService emailService,
    IOptions<EmailOptions> emailOptions,
    IOptions<AdminOptions> adminOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var (player, mine, error) = await gameService.RegisterAsync(request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var token = tokenService.GenerateToken(player!.Id, player.Username);
        return Ok(new AuthResponse(token, player.Id, mine!.Id, player.Username, FeatureFlags.Phase1));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var (player, error) = await gameService.AuthenticateAsync(request, ct);
        if (error is not null)
        {
            return Unauthorized(new { message = error });
        }

        var banMessage = await playerBanService.GetActiveBanMessageAsync(player!.Id, ct);
        if (banMessage is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = banMessage, code = "banned" });
        }

        var mineId = await gameService.GetPrimaryMineIdAsync(player.Id, ct);
        if (mineId is null || mineId == Guid.Empty)
        {
            return BadRequest(new { message = "No active mine found for player." });
        }

        var token = tokenService.GenerateToken(player.Id, player.Username);
        var announcements = await specialEventService.GetLoginAnnouncementsAsync(player.Id, ct);
        return Ok(new AuthResponse(token, player.Id, mineId.Value, player.Username, FeatureFlags.Phase1, announcements));
    }

    [AllowAnonymous]
    [HttpPost("ban-appeal")]
    public async Task<ActionResult<MessageResponse>> SubmitBanAppeal(BanAppealRequest request, CancellationToken ct)
    {
        var (appeal, player, ban, error) = await banAppealService.SubmitAppealAsync(
            request.Username,
            request.Password,
            request.Message,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var adminPortalUrl = $"{emailOptions.Value.AppBaseUrl.TrimEnd('/')}/admin";
        var banSummary = ban!.IsPermanent
            ? "Life ban"
            : $"{ban.BanLevelLabel} until {ban.ExpiresAt:yyyy-MM-dd HH:mm} UTC";

        var adminRecipients = await GetAdminRecipientsAsync(ct);
        if (adminRecipients.Count == 0)
        {
            logger.LogWarning("Ban appeal submitted but no admin recipients could be resolved.");
        }

        var emailFailures = 0;
        foreach (var admin in adminRecipients)
        {
            try
            {
                await emailService.SendBanAppealToAdminAsync(
                    admin.Email,
                    admin.Username,
                    player!.Username,
                    player.Email,
                    banSummary,
                    appeal!.Message,
                    adminPortalUrl,
                    ct);
            }
            catch (Exception ex)
            {
                emailFailures++;
                logger.LogError(ex, "Ban appeal email failed for admin {Email}", admin.Email);
            }
        }

        if (adminRecipients.Count > 0 && emailFailures == adminRecipients.Count)
        {
            return StatusCode(503, new
            {
                message = "Your appeal was saved, but admin notification email could not be sent. Verify SMTP settings and restart the API."
            });
        }

        return Ok(new MessageResponse("Your ban removal request was sent to the admin team."));
    }

    [Authorize]
    [HttpGet("session")]
    public async Task<ActionResult<SessionResponse>> Session(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            return Unauthorized(new { message = "Session expired. Sign in again." });
        }

        var banMessage = await playerBanService.GetActiveBanMessageAsync(playerId, ct);
        if (banMessage is not null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = banMessage, code = "banned" });
        }

        var mineId = await gameService.GetPrimaryMineIdAsync(playerId, ct);
        if (mineId is null || mineId == Guid.Empty)
        {
            return BadRequest(new { message = "No active mine found for player." });
        }

        var announcements = await specialEventService.GetLoginAnnouncementsAsync(playerId, ct);
        return Ok(new SessionResponse(playerId, mineId.Value, User.GetUsername() ?? string.Empty, announcements));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var (resetToken, player, error) = await gameService.CreatePasswordResetTokenAsync(request.Email, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (resetToken is not null && player is not null)
        {
            var baseUrl = emailOptions.Value.AppBaseUrl.TrimEnd('/');
            var resetUrl = $"{baseUrl}/?reset={Uri.EscapeDataString(resetToken)}";
            try
            {
                await emailService.SendPasswordResetAsync(player.Email, player.Username, resetUrl, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Password reset email failed for {Email}", player.Email);
                return StatusCode(503, new
                {
                    message = "Could not send the reset email. Verify SMTP settings in appsettings.Development.json and restart the API."
                });
            }
        }

        return Ok(new MessageResponse(PlayerGameService.PasswordResetSentMessage));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var error = await gameService.ResetPasswordAsync(request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new MessageResponse("Password updated. You can log in with your new password."));
    }

    private async Task<IReadOnlyList<(string Username, string Email)>> GetAdminRecipientsAsync(CancellationToken ct)
    {
        var configured = adminOptions.Value.Usernames ?? [];
        if (configured.Length == 0)
        {
            return [];
        }

        var lowered = configured
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var players = await db.Players.AsNoTracking()
            .Where(p => lowered.Contains(p.Username.ToLower()))
            .Select(p => new { p.Username, p.Email })
            .ToListAsync(ct);

        return players
            .Where(p => !string.IsNullOrWhiteSpace(p.Email))
            .Select(p => (p.Username, p.Email))
            .ToList();
    }
}

public static class ControllerExtensions
{
    public static Guid GetPlayerId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("playerId") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim.Value) : Guid.Empty;
    }

    public static string? GetUsername(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Name)?.Value
        ?? user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value;
}
