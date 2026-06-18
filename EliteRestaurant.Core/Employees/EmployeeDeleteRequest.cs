using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Employees;

public sealed class EmployeeDeleteRequest
{
    public Employee Employee { get; set; } = new();
    public string EmployeeDeletePasscode { get; set; } = string.Empty;
    public string? ConfirmSignInId { get; set; }
    public string? ConfirmPin { get; set; }
}
