using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Api.Services.Foreverfall;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/public/foreverfall")]
public class ForeverfallController(ForeverfallPenitentiaryService penitentiaryService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<ForeverfallRosterDto>> GetRoster(
        [FromQuery] string? date,
        CancellationToken ct)
    {
        DateOnly rosterDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            rosterDate = UtcGameClock.Today;
        }
        else if (!DateOnly.TryParse(date, out rosterDate))
        {
            return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
        }

        return Ok(await penitentiaryService.GetRosterAsync(rosterDate, ct));
    }

    [AllowAnonymous]
    [HttpGet("archives")]
    public ActionResult<ForeverfallArchivesDto> ListArchives() =>
        Ok(penitentiaryService.ListArchives());

    [AllowAnonymous]
    [HttpGet("search")]
    public ActionResult<ForeverfallSearchResultDto> Search([FromQuery] string? q) =>
        Ok(penitentiaryService.SearchInmates(q));

    [AllowAnonymous]
    [HttpGet("inmate/{inmateId}")]
    public ActionResult<ForeverfallInmateDto> GetInmate(string inmateId)
    {
        var inmate = penitentiaryService.TryGetInmate(inmateId);
        return inmate is null ? NotFound() : Ok(inmate);
    }

    [AllowAnonymous]
    [HttpGet("{date}")]
    public async Task<ActionResult<ForeverfallRosterDto>> GetRosterByDate(string date, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var rosterDate))
        {
            return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
        }

        return Ok(await penitentiaryService.GetRosterAsync(rosterDate, ct));
    }
}
