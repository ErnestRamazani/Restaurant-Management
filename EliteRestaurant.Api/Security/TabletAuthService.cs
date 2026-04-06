using System.Collections.Concurrent;
using EliteRestaurantPro.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Security;

public sealed class TabletAuthService
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(12);
    private readonly ConcurrentDictionary<string, AuthenticatedStaffSession> _sessions = new(StringComparer.Ordinal);

    public AuthenticatedStaffSession? Login(string staffId, string pin, string portal)
    {
        var id = (staffId ?? string.Empty).Trim();
        var normalizedPin = (pin ?? string.Empty).Trim();
        var normalizedPortal = (portal ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(normalizedPin))
            return null;

        using var db = new AppDbContext();
        var candidates = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .AsEnumerable()
            .Where(e => string.Equals((e.PinCode ?? string.Empty).Trim(), normalizedPin, StringComparison.Ordinal))
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) &&
                 e.SignInId.Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
                || (e.UniqueId ?? string.Empty).Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var employee = ResolvePortalCandidate(candidates, normalizedPortal);
        if (employee is null)
            return null;

        var token = Guid.NewGuid().ToString("N");
        var session = new AuthenticatedStaffSession(
            Token: token,
            EmployeeId: employee.Id,
            EmployeeUniqueId: employee.UniqueId,
            Name: employee.Name,
            Role: employee.Role,
            SignInId: employee.SignInId,
            ExpiresAtUtc: DateTime.UtcNow.Add(SessionDuration));

        _sessions[token] = session;
        CleanupExpiredSessions();
        return session;
    }

    public AuthenticatedStaffSession? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (!_sessions.TryGetValue(token.Trim(), out var session))
            return null;

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(session.Token, out _);
            return null;
        }

        return session;
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                _sessions.TryRemove(pair.Key, out _);
        }
    }

    private static EliteRestaurantPro.Models.Employee? ResolvePortalCandidate(
        IReadOnlyList<EliteRestaurantPro.Models.Employee> candidates,
        string portal)
    {
        if (string.Equals(portal, "Cashier", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(e =>
                e.Role.Equals("Cashier", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(portal, "KitchenBar", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(e => IsKitchenBarRole(e.Role));
        }

        // Default to Server portal.
        return candidates.FirstOrDefault(e =>
            e.Role.Equals("Server", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKitchenBarRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var normalized = role.Trim();
        return normalized.Equals("Chef", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("Barman", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("Bartender", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("Sous Chef", StringComparison.OrdinalIgnoreCase);
    }
}
