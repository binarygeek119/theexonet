using Theexonet.Api.Services;

namespace Theexonet.Api.Middleware;

public class StartupReadinessMiddleware(RequestDelegate next)
{
    private static readonly PathString StatusPrefix = new("/api/status");

    public async Task InvokeAsync(HttpContext context, StartupReadiness readiness)
    {
        if (readiness.IsDatabaseReady || context.Request.Path.StartsWithSegments(StatusPrefix))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";
        await context.Response.WriteAsJsonAsync(new
        {
            status = "starting",
            message = "Database schema is still applying. Retry shortly.",
            service = "Theexonet.Api"
        });
    }
}
