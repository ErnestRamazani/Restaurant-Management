using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Employees;

public static class EmployeeRoleHelper
{
    public const string OtherRole = "Other";

    public static bool IsOtherRole(string? role) =>
        (role ?? string.Empty).Trim().Equals(OtherRole, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tablet/floor/kitchen roles that require a sign-in ID for staff portals.</summary>
    public static bool RequiresTabletPortalSignInId(string? role)
    {
        if (IsOtherRole(role))
            return false;

        var r = (role ?? string.Empty).Trim();
        return r.Equals("Server", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Front desk", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Chef", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Barman", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>All canonical roles except Other may store sign-in ID and PIN.</summary>
    public static bool AllowsPortalCredentials(string? role) => !IsOtherRole(role);

    public static string ResolveSignInIdForSave(bool isOtherRole, string normalizedSignIn, string? existingSignInId)
    {
        if (isOtherRole)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedSignIn))
            return normalizedSignIn.Trim();
        return (existingSignInId ?? string.Empty).Trim();
    }

    public static string DisplayRole(Employee employee)
    {
        if (IsOtherRole(employee.Role) && !string.IsNullOrWhiteSpace(employee.CustomRoleTitle))
            return employee.CustomRoleTitle.Trim();
        return employee.Role;
    }
}
