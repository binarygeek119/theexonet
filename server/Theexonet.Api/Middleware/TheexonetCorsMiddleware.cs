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

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            TheexonetCors.ApplyHeaders(context, includePreflight: true);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
        TheexonetCors.ApplyHeaders(context);
    }
}
