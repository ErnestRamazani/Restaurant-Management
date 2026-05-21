using System.Text.Json.Serialization;

namespace EliteRestaurant.Contracts.Setup;

public sealed record SetupStatusDto(
    [property: JsonPropertyName("setupRequired")] bool SetupRequired,
    [property: JsonPropertyName("restaurantCount")] int RestaurantCount,
    [property: JsonPropertyName("message")] string Message);

public sealed record SiteSetupRequest(
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("customDomain")] string? CustomDomain,
    [property: JsonPropertyName("adminSignInId")] string AdminSignInId,
    [property: JsonPropertyName("adminPin")] string AdminPin,
    [property: JsonPropertyName("adminName")] string? AdminName,
    [property: JsonPropertyName("preferredLanguage")] string? PreferredLanguage);

public sealed record SiteSetupResponse(
    [property: JsonPropertyName("restaurantId")] int RestaurantId,
    [property: JsonPropertyName("restaurantUniqueId")] string RestaurantUniqueId,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("customDomain")] string? CustomDomain,
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("employeeId")] int EmployeeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("signInId")] string SignInId,
    [property: JsonPropertyName("role")] string Role);

public sealed record SiteSetupErrorDto(
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);
