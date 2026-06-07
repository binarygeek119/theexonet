namespace Theexonet.Api.Middleware;

/// <summary>Cross-origin rules for theexonet.com and all subdomains calling api.theexonet.com.</summary>
public static class TheexonetCors
{
    public static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1" or "[::1]")
        {
            return true;
        }

        return host == "theexonet.com" || host.EndsWith(".theexonet.com", StringComparison.Ordinal);
    }

    public static void ApplyHeaders(HttpContext context, bool includePreflight = false)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!IsAllowedOrigin(origin))
        {
            return;
        }

        var headers = context.Response.Headers;
        if (string.IsNullOrEmpty(headers.AccessControlAllowOrigin))
        {
            headers.AccessControlAllowOrigin = origin;
        }

        if (!headers.Vary.ToString().Contains("Origin", StringComparison.OrdinalIgnoreCase))
        {
            headers.Append("Vary", "Origin");
        }

        if (includePreflight)
        {
            headers.AccessControlAllowMethods = "GET, POST, PUT, PATCH, DELETE, OPTIONS";
            headers.AccessControlAllowHeaders = "Authorization, Content-Type, Accept";
        }
    }
}
