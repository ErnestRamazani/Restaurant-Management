using EliteRestaurant.Core.Tenancy;
using System.Security.Claims;

namespace EliteRestaurant.Api.Middleware;

/// <summary>Ensures JWT <c>restaurantId</c> matches the host-resolved tenant when both are present.</summary>
public sealed class TenantJwtAlignmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        if (tenant.IsResolved
            && context.User.Identity?.IsAuthenticated == true
            && int.TryParse(context.User.FindFirstValue("restaurantId"), out var claimRestaurantId)
            && claimRestaurantId > 0
            && claimRestaurantId != tenant.RestaurantId)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "This account belongs to a different restaurant site." });
            return;
        }

        await next(context);
    }
}
