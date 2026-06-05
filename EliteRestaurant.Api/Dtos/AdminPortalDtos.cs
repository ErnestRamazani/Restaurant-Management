namespace EliteRestaurant.Api.Dtos;

public sealed record AdminPortalConfigDto(
    string RestaurantName,
    string RestaurantLogoUrl,
    string? AdminWebSignInId = null,
    string? RestaurantTimeZoneId = null);

public sealed record AdminPortalLoginHintDto(string? AdminWebSignInId);
