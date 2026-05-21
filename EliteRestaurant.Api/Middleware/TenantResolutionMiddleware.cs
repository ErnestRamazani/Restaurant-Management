using EliteRestaurant.Api.Tenancy;
using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Api.Middleware;

/// <summary>Resolves the current restaurant from the request host (custom domain) or dev headers.</summary>
public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    RestaurantTenantResolver resolver)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await next(context);
            return;
        }

        var allowFallback = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var restaurant = await resolver.ResolveAsync(
            context.Request.Host.Value,
            context.Request.Headers,
            allowFallback,
            context.RequestAborted);

        if (restaurant is null)
        {
            if (RequiresTenant(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Unknown restaurant site. Use a registered domain or X-Restaurant-Id / X-Restaurant-Slug in development."
                });
                return;
            }

            await next(context);
            return;
        }

        tenant.SetRestaurant(restaurant, context.Request.Host.Value ?? string.Empty);
        await next(context);
    }

    private static bool ShouldSkip(PathString path) =>
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresTenant(PathString path)
    {
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        return path.StartsWithSegments("/api/public", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/server", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/cashier", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/kitchen", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/bar", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/reception", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tables", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/floor", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/language", StringComparison.OrdinalIgnoreCase);
    }
}
