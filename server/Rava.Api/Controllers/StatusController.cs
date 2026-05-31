using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Infrastructure.Data;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api")]
public class StatusController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<ActionResult<ApiStatusResponse>> Get(CancellationToken ct)
    {
        var databaseConnected = false;
        try
        {
            databaseConnected = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            // Report offline database status when the server is unreachable.
        }

        var databaseStatus = databaseConnected ? "online" : "offline";
        var status = databaseConnected ? "online" : "degraded";

        return Ok(new ApiStatusResponse(
            status,
            "Rava.Api",
            DateTime.UtcNow,
            databaseConnected,
            databaseStatus));
    }
}
