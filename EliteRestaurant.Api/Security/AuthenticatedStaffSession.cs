namespace EliteRestaurant.Api.Security;

public sealed record AuthenticatedStaffSession(
    string Token,
    int EmployeeId,
    int RestaurantId,
    string EmployeeUniqueId,
    string Name,
    string Role,
    string SignInId,
    string Portal,
    DateTime ExpiresAtUtc);
