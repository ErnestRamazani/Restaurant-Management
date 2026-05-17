using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Staff;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Security;

public sealed class TabletAuthService(AppDbContext db, JwtTokenService jwtTokenService)
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(12);

    public TabletLoginOutcome Login(string staffId, string pin, string portal)
    {
        var id = (staffId ?? string.Empty).Trim();
        var normalizedPin = (pin ?? string.Empty).Trim();
        var normalizedPortal = (portal ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(normalizedPin))
            return new TabletLoginOutcome(null, "Enter both sign-in ID and PIN.");

        // Admin / AdminWeb: allow match on sign-in ID, unique ID, or display name (desktop admin screen).
        // Other portals: sign-in ID or unique ID only (tablets).
        var idMatches = string.Equals(normalizedPortal, "Admin", StringComparison.OrdinalIgnoreCase)
            ? StaffPortalAuthentication.QueryActiveAdminPortalCandidates(db.Employees.AsNoTracking(), id).ToList()
            : string.Equals(normalizedPortal, "AdminWeb", StringComparison.OrdinalIgnoreCase)
                ? StaffPortalAuthentication.QueryActiveAdminWebPortalCandidates(db.Employees.AsNoTracking(), id).ToList()
                : StaffPortalAuthentication.QueryActiveEmployeesMatchingStaffId(db.Employees.AsNoTracking(), id).ToList();
        if (idMatches.Count == 0)
        {
            return new TabletLoginOutcome(null,
                string.Equals(normalizedPortal, "AdminWeb", StringComparison.OrdinalIgnoreCase)
                    ? "No active AdminWeb user matches that sign-in ID. Use er4124 after the API has restarted on production (seeding), or add an employee with role AdminWeb in Elite Pro."
                    : string.Equals(normalizedPortal, "Admin", StringComparison.OrdinalIgnoreCase)
                        ? "No active Admin or Manager matches that ID, employee code, or name."
                        : "No active employee matches that sign-in ID.");
        }

        var candidates = StaffPortalAuthentication.FilterPinMatches(idMatches, normalizedPin);
        if (candidates.Count == 0)
            return new TabletLoginOutcome(null, "PIN is incorrect.");

        var employee = StaffPortalAuthentication.ResolvePortalCandidate(candidates, normalizedPortal);
        if (employee is null)
        {
            return new TabletLoginOutcome(null,
                string.Equals(normalizedPortal, "AdminWeb", StringComparison.OrdinalIgnoreCase)
                    ? "This account is not an AdminWeb user. Use an employee with role AdminWeb (or the seeded er4124 account)."
                    : "This account cannot use the selected portal.");
        }

        CleanupExpiredSessions(db);

        var token = Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.UtcNow.Add(SessionDuration);
        var canonicalPortal = StaffPortalAuthentication.CanonicalPortalForEmployee(employee);

        db.TabletSessions.Add(new TabletSession
        {
            Token = token,
            EmployeeId = employee.Id,
            Portal = canonicalPortal,
            EmployeeUniqueId = employee.UniqueId ?? string.Empty,
            Name = employee.Name,
            Role = employee.Role,
            SignInId = employee.SignInId ?? string.Empty,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();

        return new TabletLoginOutcome(ToAuthenticatedSession(token, employee, canonicalPortal, expiresAtUtc), null);
    }

    public AuthenticatedStaffSession? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var t = token.Trim();
        var jwtSession = jwtTokenService.ValidateToken(t);
        if (jwtSession is not null)
            return jwtSession;

        var row = db.TabletSessions.FirstOrDefault(s => s.Token == t);
        if (row is null)
            return null;

        if (row.ExpiresAtUtc <= DateTime.UtcNow)
        {
            db.TabletSessions.Remove(row);
            db.SaveChanges();
            return null;
        }

        return new AuthenticatedStaffSession(
            row.Token,
            row.EmployeeId,
            row.EmployeeUniqueId,
            row.Name,
            row.Role,
            row.SignInId,
            row.Portal,
            row.ExpiresAtUtc);
    }

    private static AuthenticatedStaffSession ToAuthenticatedSession(
        string token,
        Employee employee,
        string canonicalPortal,
        DateTime expiresAtUtc) =>
        new(
            token,
            employee.Id,
            employee.UniqueId ?? string.Empty,
            employee.Name,
            employee.Role,
            employee.SignInId ?? string.Empty,
            canonicalPortal,
            expiresAtUtc);

    private static void CleanupExpiredSessions(AppDbContext db)
    {
        var utcNow = DateTime.UtcNow;
        var stale = db.TabletSessions.Where(s => s.ExpiresAtUtc <= utcNow).ToList();
        if (stale.Count == 0)
            return;
        db.TabletSessions.RemoveRange(stale);
    }
}
