namespace EliteRestaurant.Api.Middleware;

/// <summary>Forces JWT <c>portal=AdminWeb</c> to safe methods only on <c>/api/*</c> (except <c>/api/auth</c>).</summary>
public sealed class AdminWebReadOnlyApiMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var portal = context.User.FindFirst("portal")?.Value;
        if (!string.Equals(portal, "AdminWeb", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (SafeMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Read-only web admin cannot modify data through this API."
        });
    }
}
