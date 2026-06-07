namespace Theexonet.Api.Middleware;

/// <summary>Echoes the request Origin for allowed theexonet.com hosts (no wildcard *).</summary>
public class TheexonetCorsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!TheexonetCors.IsAllowedOrigin(origin))
        {
            await next(context);
            return;
        }

        var isOptions = HttpMethods.IsOptions(context.Request.Method);
        context.Response.OnStarting(() =>
        {
            TheexonetCors.ApplyHeaders(context, includePreflight: isOptions);
            return Task.CompletedTask;
        });

        if (isOptions)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
    }
}
