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

        var idMatches = StaffPortalAuthentication
            .QueryActiveEmployeesMatchingStaffId(db.Employees.AsNoTracking(), id)
            .ToList();
        if (idMatches.Count == 0)
        {
            return new TabletLoginOutcome(null,
                "No active employee matches that sign-in ID. For the default web admin account use er4124 after the API has applied database migrations and startup seeding, or ask an administrator to add an AdminWeb user.");
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
