using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Api.Services.LunarWeather;
using Theexonet.Core.Dtos;
using Theexonet.Core.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/public/lunar-weather")]
public class LunarWeatherController(LunarWeatherService lunarWeatherService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<LunarWeatherBulletinDto>> GetBulletin(
        [FromQuery] string? date,
        CancellationToken ct)
    {
        DateOnly bulletinDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            bulletinDate = UtcGameClock.Today;
        }
        else if (!DateOnly.TryParse(date, out bulletinDate))
        {
            return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
        }

        return Ok(await lunarWeatherService.GetBulletinAsync(bulletinDate, ct));
    }

    [AllowAnonymous]
    [HttpGet("archives")]
    public ActionResult<LunarWeatherArchivesDto> ListArchives() =>
        Ok(lunarWeatherService.ListArchives());
}
