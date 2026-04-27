using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Staff;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Security;

public sealed class TabletAuthService(AppDbContext db)
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(12);

    public AuthenticatedStaffSession? Login(string staffId, string pin, string portal)
    {
        var id = (staffId ?? string.Empty).Trim();
        var normalizedPin = (pin ?? string.Empty).Trim();
        var normalizedPortal = (portal ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(normalizedPin))
            return null;

        var idMatches = StaffPortalAuthentication
            .QueryActiveEmployeesMatchingStaffId(db.Employees.AsNoTracking(), id)
            .ToList();
        var candidates = StaffPortalAuthentication.FilterPinMatches(idMatches, normalizedPin);
        var employee = StaffPortalAuthentication.ResolvePortalCandidate(candidates, normalizedPortal);
        if (employee is null)
            return null;

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

        return ToAuthenticatedSession(token, employee, canonicalPortal, expiresAtUtc);
    }

    public AuthenticatedStaffSession? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var t = token.Trim();
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
        db.TabletSessions.Where(s => s.ExpiresAtUtc <= utcNow).ExecuteDelete();
    }
}
