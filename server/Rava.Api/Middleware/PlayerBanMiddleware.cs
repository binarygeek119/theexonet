using Rava.Api.Controllers;
using Rava.Infrastructure.Services;

namespace Rava.Api.Middleware;

public class PlayerBanMiddleware(RequestDelegate next)
{
    private static readonly PathString AdminPrefix = new("/api/admin");
    private static readonly PathString ModeratorPrefix = new("/api/moderator");

    public async Task InvokeAsync(HttpContext context, PlayerBanService playerBanService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments(AdminPrefix) && !path.StartsWithSegments(ModeratorPrefix))
            {
                var playerId = context.User.GetPlayerId();
                if (playerId != Guid.Empty)
                {
                    var banMessage = await playerBanService.GetActiveBanMessageAsync(playerId, context.RequestAborted);
                    if (banMessage is not null)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { message = banMessage });
                        return;
                    }
                }
            }
        }

        await next(context);
    }
}
