using System.Text.Json.Serialization;

namespace EliteRestaurant.Contracts.Auth;

public sealed record CloudLoginRequest(string StaffId, string Pin, string Portal);

/// <param name="Response">Set when the API returned 200 with a body.</param>
/// <param name="ErrorMessage">API or transport detail when <see cref="Response"/> is null (e.g. invalid credentials).</param>
public sealed record CloudAuthResult(CloudLoginResponse? Response, string? ErrorMessage);

public sealed record CloudLoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("employeeId")] int EmployeeId,
    [property: JsonPropertyName("employeeUniqueId")] string EmployeeUniqueId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("signInId")] string SignInId,
    [property: JsonPropertyName("portal")] string Portal);
