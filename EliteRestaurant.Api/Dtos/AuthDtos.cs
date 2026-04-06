namespace EliteRestaurant.Api.Dtos;

public sealed record LoginRequest(string StaffId, string Pin, string Portal = "Server");

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    int EmployeeId,
    string EmployeeUniqueId,
    string Name,
    string Role,
    string SignInId);
