using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mines")]
public class MineController(PlayerGameService gameService, CompanyCrewService companyCrewService) : ControllerBase
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

    [HttpGet("{mineId:guid}/shipping")]
    public async Task<ActionResult<ShippingDashboardResponse>> GetShipping(Guid mineId, CancellationToken ct)
    {
        var dashboard = await gameService.GetShippingDashboardForMineAsync(User.GetPlayerId(), mineId, ct);
        return Ok(dashboard);
    }

    [HttpPost("{mineId:guid}/shipping/schedule")]
    public async Task<ActionResult<ActionResponse>> ScheduleShipment(
        Guid mineId, ScheduleShipmentRequest request, CancellationToken ct)
    {
        var (success, message, shipmentId) = await gameService.ScheduleShipmentAsync(
            User.GetPlayerId(), mineId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpDelete("{mineId:guid}/shipping/{shipmentId:guid}")]
    public async Task<ActionResult<ActionResponse>> CancelShipment(
        Guid mineId, Guid shipmentId, CancellationToken ct)
    {
        var (success, message) = await gameService.CancelShipmentAsync(
            User.GetPlayerId(), mineId, shipmentId, ct);
        return success
            ? Ok(new ActionResponse(true, message))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/workers/hire")]
    public async Task<ActionResult<ActionResponse>> HireWorker(
        Guid mineId, HireWorkerRequest? request, CancellationToken ct)
    {
        var (success, message, completions) = await companyCrewService.HireWorkerAsync(
            User.GetPlayerId(), mineId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/workers/{workerId:guid}/fire")]
    public async Task<ActionResult<ActionResponse>> FireWorker(
        Guid mineId, Guid workerId, CancellationToken ct)
    {
        var (success, message, completions) = await companyCrewService.FireWorkerAsync(
            User.GetPlayerId(), mineId, workerId, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/workers/{workerId:guid}/layoff")]
    public async Task<ActionResult<ActionResponse>> LayoffWorker(
        Guid mineId, Guid workerId, CancellationToken ct)
    {
        var (success, message, completions) = await companyCrewService.LayoffWorkerAsync(
            User.GetPlayerId(), mineId, workerId, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/workers/{workerId:guid}/raise")]
    public async Task<ActionResult<ActionResponse>> RaiseWorker(
        Guid mineId, Guid workerId, RaiseWorkerRequest request, CancellationToken ct)
    {
        var (success, message, completions) = await companyCrewService.RaiseWorkerAsync(
            User.GetPlayerId(), mineId, workerId, request, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }

    [HttpPost("{mineId:guid}/mining-rights/renew")]
    public async Task<ActionResult<ActionResponse>> RenewMiningRights(
        Guid mineId, CancellationToken ct)
    {
        var (success, message, completions) = await companyCrewService.RenewMiningRightsAsync(
            User.GetPlayerId(), mineId, ct);
        return success
            ? Ok(new ActionResponse(true, message, null, completions))
            : BadRequest(new ActionResponse(false, message));
    }
}
