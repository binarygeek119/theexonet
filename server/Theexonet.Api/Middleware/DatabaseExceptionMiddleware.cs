using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Theexonet.Api.Middleware;

public class DatabaseExceptionMiddleware(RequestDelegate next, ILogger<DatabaseExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (IsDatabaseException(ex))
        {
            logger.LogError(ex, "Database error during request {Method} {Path}", context.Request.Method, context.Request.Path);
            TheexonetCors.ApplyHeaders(context);
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Database unavailable. Check PostgreSQL connection settings in appsettings.json."
            });
        }
    }

    private static bool IsDatabaseException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException or NpgsqlException or DbUpdateException)
            {
                return true;
            }

            if (current is InvalidOperationException &&
                current.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current is TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
