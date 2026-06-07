using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class GameController(PlayerGameService gameService) : ControllerBase
{
    [HttpPost("game/advance-day")]
    public async Task<ActionResult<DayAdvanceResponse>> AdvanceDay(CancellationToken ct)
    {
        var (result, completions) = await gameService.AdvanceDayAsync(User.GetPlayerId(), ct);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result with { EventCompletions = completions.Count > 0 ? completions : null });
    }

    [HttpGet("market/today")]
    public async Task<ActionResult<MarketTodayResponse>> GetMarketToday(CancellationToken ct)
    {
        var result = await gameService.GetMarketTodayAsync(User.GetPlayerId(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("player/finances")]
    public async Task<ActionResult<FinanceResponse>> GetFinances(CancellationToken ct)
    {
        var result = await gameService.GetFinancesAsync(User.GetPlayerId(), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
