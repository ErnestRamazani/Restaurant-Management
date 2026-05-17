using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Ensures the documented default read-only web admin row exists after migrations (role <c>AdminWeb</c>).
/// Business settings and public menu branding are changed only from the desktop app (Admin role).
/// </summary>
public static class AdminWebLoginSeed
{
    public const string SeedUniqueId = "EMP-SEED-ADMINWEB";
    public const string SeedSignInId = "er4124";
    public const string DefaultPinPlaintext = "4124";

    public static void EnsureSeeded(AppDbContext db)
    {
        var existing = db.Employees.FirstOrDefault(e => e.UniqueId == SeedUniqueId);
        if (existing is not null)
        {
            if (RepairSeedRow(existing))
                db.SaveChanges();
            return;
        }

        var lower = SeedSignInId.ToLowerInvariant();
        var signInConflict = db.Employees.FirstOrDefault(e =>
            !string.IsNullOrWhiteSpace(e.SignInId)
            && e.SignInId.Trim().ToLowerInvariant() == lower);
        if (signInConflict is not null)
        {
            if (RepairSeedRow(signInConflict))
                db.SaveChanges();
            return;
        }

        db.Employees.Add(new Employee
        {
            UniqueId = SeedUniqueId,
            SignInId = SeedSignInId,
            Name = "Web Admin (seed)",
            Role = "AdminWeb",
            PinCode = EmployeePinHasher.HashForStorage(DefaultPinPlaintext),
            ProfileImagePath = string.Empty,
            PhoneNumber = string.Empty,
            HourlyRate = 0m,
            MonthlySalaryUSD = 0m,
            JoinDate = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc),
            EmploymentStatus = "Active",
            Notes = "Seed account for read-only admin web; rotate from Manager desktop or SQL.",
            MondayShift = "Off",
            TuesdayShift = "Off",
            WednesdayShift = "Off",
            ThursdayShift = "Off",
            FridayShift = "Off",
            SaturdayShift = "Off",
            SundayShift = "Off"
        });

        db.SaveChanges();
    }

    private static bool RepairSeedRow(Employee employee)
    {
        var changed = false;
        if (!employee.Role.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase))
        {
            employee.Role = "AdminWeb";
            changed = true;
        }

        if (!string.Equals(employee.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            employee.EmploymentStatus = "Active";
            changed = true;
        }

        if (!string.Equals(employee.SignInId?.Trim(), SeedSignInId, StringComparison.OrdinalIgnoreCase))
        {
            employee.SignInId = SeedSignInId;
            changed = true;
        }

        if (!EmployeePinHasher.Verify(DefaultPinPlaintext, employee.PinCode))
        {
            employee.PinCode = EmployeePinHasher.HashForStorage(DefaultPinPlaintext);
            changed = true;
        }

        var notes = employee.Notes ?? string.Empty;
        if (!notes.Contains("read-only admin web", StringComparison.OrdinalIgnoreCase))
        {
            employee.Notes = "Seed account for read-only admin web; rotate from Manager desktop or SQL.";
            changed = true;
        }

        return changed;
    }
}
