using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Api.Services.OffworldNews;
using Rava.Core.Dtos;
using Rava.Core.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/public/offworld-news")]
public class OffworldNewsController(OffworldNewsService offworldNewsService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<OffworldNewsEditionDto>> GetEdition(
        [FromQuery] string? date,
        CancellationToken ct)
    {
        DateOnly editionDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            editionDate = UtcGameClock.Today;
        }
        else if (!DateOnly.TryParse(date, out editionDate))
        {
            return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
        }

        return Ok(await offworldNewsService.GetEditionAsync(editionDate, ct));
    }

    [AllowAnonymous]
    [HttpGet("archives")]
    public ActionResult<OffworldNewsArchivesDto> ListArchives()
    {
        return Ok(offworldNewsService.ListArchives());
    }
}
