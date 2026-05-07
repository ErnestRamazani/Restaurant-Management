namespace EliteRestaurant.Contracts.Auth;

public sealed record CloudLoginRequest(string StaffId, string Pin, string Portal);

public sealed record CloudLoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    int EmployeeId,
    string EmployeeUniqueId,
    string Name,
    string Role,
    string SignInId);
