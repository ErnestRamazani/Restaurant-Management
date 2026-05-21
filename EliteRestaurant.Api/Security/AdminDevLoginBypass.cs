using EliteRestaurant.Core.Data;
using EliteRestaurant.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EliteRestaurant.Api.Security;

internal static class AdminDevLoginBypass
{
    /// <summary>
    /// Desktop admin login: accept any credentials only when <see cref="AuthDevOptions.DesktopAdminAcceptAnyCredentials"/> is explicitly enabled (never in Development by default).
    /// </summary>
    public static AuthenticatedStaffSession? TryCreateSession(
        CloudLoginRequest request,
        AppDbContext db,
        IOptions<AuthDevOptions> authDevOptions)
    {
        if (!string.Equals(request.Portal, "Admin", StringComparison.OrdinalIgnoreCase))
            return null;

        var opts = authDevOptions.Value;
        if (!opts.DesktopAdminAcceptAnyCredentials)
            return null;

        var staffId = (request.StaffId ?? string.Empty).Trim();
        var pin = (request.Pin ?? string.Empty).Trim();
        if (staffId.Length == 0 || pin.Length == 0)
            return null;

        var impersonate = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active"
                        && (e.Role == "Admin" || e.Role == "Manager"))
            .OrderBy(e => e.Id)
            .FirstOrDefault();

        var expiresAtUtc = DateTime.UtcNow.AddHours(12);
        if (impersonate is not null)
        {
            return new AuthenticatedStaffSession(
                "dev-admin-bypass",
                impersonate.Id,
                impersonate.UniqueId ?? string.Empty,
                impersonate.Name,
                impersonate.Role,
                string.IsNullOrWhiteSpace(impersonate.SignInId) ? staffId : impersonate.SignInId,
                "Admin",
                expiresAtUtc);
        }

        return new AuthenticatedStaffSession(
            "dev-admin-bypass",
            0,
            "DEV-ADMIN",
            string.IsNullOrWhiteSpace(staffId) ? "Dev Admin" : staffId,
            "Admin",
            staffId,
            "Admin",
            expiresAtUtc);
    }
}
