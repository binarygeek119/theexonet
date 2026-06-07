using Theexonet.Api.Controllers;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Middleware;

public class PlayerModerationMiddleware(RequestDelegate next)
{
    private static readonly PathString AdminPrefix = new("/api/admin");
    private static readonly PathString ModeratorPrefix = new("/api/moderator");
    private static readonly PathString StaffPrefix = new("/api/staff");
    private static readonly PathString AcknowledgeWarningPrefix = new("/api/auth/acknowledge-warning");

    public async Task InvokeAsync(
        HttpContext context,
        PlayerBanService playerBanService,
        PlayerWarningService playerWarningService,
        StaffModerationPolicy staffModerationPolicy)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path;
        if (path.StartsWithSegments(AdminPrefix)
            || path.StartsWithSegments(ModeratorPrefix)
            || path.StartsWithSegments(StaffPrefix)
            || path.StartsWithSegments(AcknowledgeWarningPrefix))
        {
            await next(context);
            return;
        }

        var username = context.User.GetUsername();
        if (staffModerationPolicy.IsStaffUsername(username))
        {
            await next(context);
            return;
        }

        var playerId = context.User.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            await next(context);
            return;
        }

        var ban = await playerBanService.GetActiveBanAsync(playerId, context.RequestAborted);
        if (ban is not null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = PlayerBanService.FormatMessage(ban),
                code = "banned",
                ban
            });
            return;
        }

        var pendingWarnings = await playerWarningService.GetUnacknowledgedWarningsAsync(
            playerId,
            context.RequestAborted);
        if (pendingWarnings.Count > 0)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "You must acknowledge your account warning before continuing. Please sign in again.",
                code = "warning_required",
                warnings = pendingWarnings
            });
            return;
        }

        await next(context);
    }
}
