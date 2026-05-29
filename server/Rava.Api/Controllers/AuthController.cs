using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Core.Models;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(PlayerGameService gameService, ITokenService tokenService) : ControllerBase
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

        var mineId = await gameService.GetPrimaryMineIdAsync(player!.Id, ct);
        if (mineId is null || mineId == Guid.Empty)
        {
            return BadRequest(new { message = "No active mine found for player." });
        }

        var token = tokenService.GenerateToken(player.Id, player.Username);
        return Ok(new AuthResponse(token, player.Id, mineId.Value, player.Username, FeatureFlags.Phase1));
    }
}

public static class ControllerExtensions
{
    public static Guid GetPlayerId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("playerId") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim.Value) : Guid.Empty;
    }
}
