using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public class GameController(PlayerGameService gameService, CosmicReserveService cosmicReserveService) : ControllerBase
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

    [HttpGet("player/reserve")]
    public async Task<ActionResult<CosmicReserveResponse>> GetCosmicReserve(CancellationToken ct)
    {
        var result = await cosmicReserveService.GetAsync(User.GetPlayerId(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("player/reserve/transfer")]
    public async Task<ActionResult<CosmicReserveResponse>> TransferReserve(
        ReserveTransferRequest request,
        CancellationToken ct)
    {
        var (result, error) = await cosmicReserveService.TransferAsync(User.GetPlayerId(), request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }
}
