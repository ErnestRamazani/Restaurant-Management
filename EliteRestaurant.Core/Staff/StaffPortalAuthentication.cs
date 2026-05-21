using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Staff;

/// <summary>Shared staff sign-in: ID match query, PIN verification, and portal/role eligibility (API + desktop).</summary>
public static class StaffPortalAuthentication
{
    public static bool IsKitchenBarRole(string? role) =>
        IsKitchenFoodRole(role) || IsBarRole(role);

    public static bool IsKitchenFoodRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var r = role.Trim();
        return r.Equals("Chef", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBarRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var r = role.Trim();
        return r.Equals("Barman", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Bartender", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsReceptionRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var r = role.Trim();
        return r.Equals("Front desk", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Cashier", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Manager", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Elite Pro desktop admin app — Admin or Manager only (not AdminWeb, Server, Cashier, etc.).</summary>
    public static bool IsAdminDesktopRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var r = role.Trim();
        return r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
               || r.Equals("Manager", StringComparison.OrdinalIgnoreCase);
    }

    public static string AdminDesktopPortalRejectedMessage(string? actualRole)
    {
        var roleLabel = string.IsNullOrWhiteSpace(actualRole) ? "unknown" : actualRole.Trim();
        return $"Only Admin or Manager accounts can sign in to Elite Pro Admin (your role: {roleLabel}). "
               + "Use Server, Cashier, Kitchen, Reception, or Bar for other roles.";
    }

    public static string CanonicalPortalForEmployee(Employee employee)
    {
        if (employee.Role.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase))
            return "AdminWeb";
        if (employee.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || employee.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            return "Admin";
        if (employee.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            return "Cashier";
        if (employee.Role.Equals("Front desk", StringComparison.OrdinalIgnoreCase))
            return "Reception";
        if (IsBarRole(employee.Role))
            return "Bar";
        if (IsKitchenFoodRole(employee.Role))
            return "Kitchen";
        return "Server";
    }

    /// <summary>Same portal rules as <see cref="CanonicalPortalForEmployee"/> for a role string (e.g. cloud login payload).</summary>
    public static string CanonicalPortalForRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Server";

        var r = role.Trim();
        if (r.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase))
            return "AdminWeb";
        if (r.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || r.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            return "Admin";
        if (r.Equals("Cashier", StringComparison.OrdinalIgnoreCase))
            return "Cashier";
        if (r.Equals("Front desk", StringComparison.OrdinalIgnoreCase))
            return "Reception";
        if (IsBarRole(r))
            return "Bar";
        if (IsKitchenFoodRole(r))
            return "Kitchen";
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
    /// Active AdminWeb employees matching sign-in ID, UniqueId, or display name (trim + case-insensitive).
    /// </summary>
    public static IQueryable<Employee> QueryActiveAdminWebPortalCandidates(IQueryable<Employee> employees, string staffId)
    {
        var trimmed = (staffId ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return employees.Where(_ => false);

        var lower = trimmed.ToLowerInvariant();
        return employees
            .Where(e => e.EmploymentStatus == "Active")
            .Where(e => e.Role.ToLower() == "adminweb")
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) && e.SignInId.Trim().ToLower() == lower)
                || e.UniqueId.Trim().ToLower() == lower
                || e.Name.Trim().ToLower() == lower);
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
            return pinMatchedCandidates.FirstOrDefault(e => IsAdminDesktopRole(e.Role));

        if (string.Equals(normalizedPortal, "AdminWeb", StringComparison.OrdinalIgnoreCase))
        {
            return pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(normalizedPortal, "Cashier", StringComparison.OrdinalIgnoreCase))
        {
            return pinMatchedCandidates.FirstOrDefault(e =>
                e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(normalizedPortal, "Reception", StringComparison.OrdinalIgnoreCase))
            return pinMatchedCandidates.FirstOrDefault(e => IsReceptionRole(e.Role));

        if (string.Equals(normalizedPortal, "Bar", StringComparison.OrdinalIgnoreCase))
            return pinMatchedCandidates.FirstOrDefault(e => IsBarRole(e.Role));

        if (string.Equals(normalizedPortal, "Kitchen", StringComparison.OrdinalIgnoreCase))
            return pinMatchedCandidates.FirstOrDefault(e => IsKitchenFoodRole(e.Role));

        if (string.Equals(normalizedPortal, "KitchenBar", StringComparison.OrdinalIgnoreCase))
            return pinMatchedCandidates.FirstOrDefault(e => IsKitchenBarRole(e.Role));

        /// <summary>Elite Menu PWA: passcode gate + optional tablet ID+PIN — return the PIN-matched employee (any role).</summary>
        if (string.Equals(normalizedPortal, "elite-menu", StringComparison.OrdinalIgnoreCase))
            return pinMatchedCandidates.FirstOrDefault();

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
