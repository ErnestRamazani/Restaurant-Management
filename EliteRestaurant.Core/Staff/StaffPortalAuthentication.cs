using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Staff;

/// <summary>Shared staff sign-in: ID match query, PIN verification, and portal/role eligibility (API + desktop).</summary>
public static class StaffPortalAuthentication
{
    public static bool IsKitchenBarRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var r = role.Trim();
        return r.Equals("Chef", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Barman", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
    }

    public static string CanonicalPortalForEmployee(Employee employee)
    {
        if (employee.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || employee.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            return "Admin";
        if (employee.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            return "Cashier";
        if (IsKitchenBarRole(employee.Role))
            return "KitchenBar";
        return "Server";
    }

    /// <summary>
    /// Active employees whose Sign-in ID or UniqueId matches <paramref name="staffId"/> (trim + case-insensitive).
    /// Intended for DB execution; PIN is applied in-memory via <see cref="FilterPinMatches"/>.
    /// </summary>
    public static IQueryable<Employee> QueryActiveEmployeesMatchingStaffId(IQueryable<Employee> employees, string staffId)
    {
        var trimmed = (staffId ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return employees.Where(_ => false);

        var lower = trimmed.ToLowerInvariant();
        return employees
            .Where(e => e.EmploymentStatus == "Active")
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) && e.SignInId.Trim().ToLower() == lower)
                || e.UniqueId.Trim().ToLower() == lower);
    }

    /// <summary>
    /// Active Admin or Manager employees matching sign-in ID, UniqueId, or display name (trim + case-insensitive).
    /// Used by desktop admin login (no PIN gate in current flow).
    /// </summary>
    public static IQueryable<Employee> QueryActiveAdminPortalCandidates(IQueryable<Employee> employees, string staffId)
    {
        var trimmed = (staffId ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return employees.Where(_ => false);

        var lower = trimmed.ToLowerInvariant();
        return employees
            .Where(e => e.EmploymentStatus == "Active")
            .Where(e =>
                e.Role.ToLower() == "admin"
                || e.Role.ToLower() == "manager")
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) && e.SignInId.Trim().ToLower() == lower)
                || e.UniqueId.Trim().ToLower() == lower
                || e.Name.Trim().ToLower() == lower);
    }

    /// <summary>BCrypt or legacy plaintext PIN check — must run client-side.</summary>
    public static List<Employee> FilterPinMatches(IEnumerable<Employee> staffIdMatches, string plainPin) =>
        staffIdMatches.Where(e => EmployeePinHasher.Verify(plainPin, e.PinCode)).ToList();

    public static Employee? ResolvePortalCandidate(IReadOnlyList<Employee> pinMatchedCandidates, string normalizedPortal)
    {
        if (pinMatchedCandidates.Count == 0)
            return null;

        if (string.Equals(normalizedPortal, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || e.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(normalizedPortal, "Cashier", StringComparison.OrdinalIgnoreCase))
        {
            return pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(normalizedPortal, "KitchenBar", StringComparison.OrdinalIgnoreCase))
        {
            return pinMatchedCandidates.FirstOrDefault(e => IsKitchenBarRole(e.Role));
        }

        return pinMatchedCandidates.FirstOrDefault(e =>
            e.Role.Equals("Server", StringComparison.OrdinalIgnoreCase));
    }

    public static Employee? ResolvePortalCandidate(IReadOnlyList<Employee> pinMatchedCandidates, StaffPortalKind portal) =>
        portal switch
        {
            StaffPortalKind.Cashier => pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase)),
            StaffPortalKind.KitchenBar => pinMatchedCandidates.FirstOrDefault(e => IsKitchenBarRole(e.Role)),
            _ => pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
        };
}
