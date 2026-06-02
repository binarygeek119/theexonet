using Rava.Api.Controllers;
using Rava.Infrastructure.Services;

namespace Rava.Api.Middleware;

public class PlayerActivityMiddleware(RequestDelegate next)
{
    private static readonly PathString AdminPrefix = new("/api/admin");
    private static readonly PathString ModeratorPrefix = new("/api/moderator");

    public async Task InvokeAsync(HttpContext context, PlayerActivityService playerActivityService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments(AdminPrefix) && !path.StartsWithSegments(ModeratorPrefix))
            {
                var playerId = context.User.GetPlayerId();
                if (playerId != Guid.Empty)
                {
                    try
                    {
                        await playerActivityService.TouchAsync(playerId, context.RequestAborted);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // Activity tracking must not block gameplay requests.
                    }
                }
            }
        }

        await next(context);
    }
}
