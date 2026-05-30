using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mines")]
public class MineController(PlayerGameService gameService) : ControllerBase
{
    [HttpGet("{mineId:guid}")]
    public async Task<ActionResult<MineDetailResponse>> GetMine(Guid mineId, CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var mine = await gameService.GetMineAsync(playerId, mineId, ct);
        return mine is null ? NotFound() : Ok(mine);
    }

    [HttpPost("{mineId:guid}/assign-worker")]
    public async Task<ActionResult<ActionResponse>> AssignWorker(
        Guid mineId, AssignWorkerRequest request, CancellationToken ct)
    {
        var (success, message, completions) = await gameService.AssignWorkerAsync(User.GetPlayerId(), mineId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/buy-supply")]
    public async Task<ActionResult<ActionResponse>> BuySupply(
        Guid mineId, BuySupplyRequest request, CancellationToken ct)
    {
        var (success, message, credits, completions) = await gameService.BuySupplyAsync(User.GetPlayerId(), mineId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message, credits, completions))
            : BadRequest(new ActionResponse(false, message, credits));
    }

    [HttpPost("{mineId:guid}/sell-ore")]
    public async Task<ActionResult<ActionResponse>> SellOre(
        Guid mineId, SellOreRequest request, CancellationToken ct)
    {
        var (success, message, credits, completions) = await gameService.SellOreAsync(User.GetPlayerId(), mineId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message, credits, completions))
            : BadRequest(new ActionResponse(false, message, credits));
    }
}
