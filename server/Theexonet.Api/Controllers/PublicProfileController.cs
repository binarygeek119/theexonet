using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/public/profiles")]
public class PublicProfileController(PublicProfileService publicProfileService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<ActionResult<PublicProfileSearchResponse>> Search(
        [FromQuery] string q,
        [FromQuery] string mode = "auto",
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Enter a username, profile number, or company name to search." });
        }

        return Ok(await publicProfileService.SearchAsync(q, mode, limit, ct));
    }

    [AllowAnonymous]
    [HttpGet("browse")]
    public async Task<ActionResult<PublicProfileBrowseResponse>> Browse(
        [FromQuery] string sort = PublicProfileService.SortUsername,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default) =>
        Ok(await publicProfileService.BrowseAsync(sort, limit, offset, ct));

    [AllowAnonymous]
    [HttpGet("leaderboard")]
    public async Task<ActionResult<PublicProfileLeaderboardResponse>> Leaderboard(
        [FromQuery] string sort = PublicProfileService.SortCompanyValue,
        [FromQuery] int limit = 25,
        CancellationToken ct = default) =>
        Ok(await publicProfileService.GetLeaderboardAsync(sort, limit, ct));

    [AllowAnonymous]
    [HttpGet("user/{username}")]
    public async Task<ActionResult<PublicProfileDetailDto>> GetByUsername(string username, CancellationToken ct)
    {
        var profile = await publicProfileService.GetByUsernameAsync(username, ct);
        return profile is null ? NotFound(new { message = "Profile not found." }) : Ok(profile);
    }
}
