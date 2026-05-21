using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Ensures the read-only web admin employee (<see cref="SeedUniqueId"/>) matches credentials from business settings.
/// Credentials are configured in Elite Pro → Appearance → Admin web portal (pushed to cloud).
/// </summary>
public static class AdminWebLoginSeed
{
    public const string SeedUniqueId = "EMP-SEED-ADMINWEB";

    public static void EnsureSeeded(AppDbContext db)
    {
        var (signInId, pin) = AdminWebSettingsResolver.Resolve(db);
        if (string.IsNullOrWhiteSpace(signInId) || string.IsNullOrWhiteSpace(pin))
            return;

        signInId = signInId.Trim();
        pin = pin.Trim();

        var existing = db.Employees.FirstOrDefault(e => e.UniqueId == SeedUniqueId);
        if (existing is not null)
        {
            if (SyncSeedRow(existing, signInId, pin))
                db.SaveChanges();
            return;
        }

        var lower = signInId.ToLowerInvariant();
        var signInConflict = db.Employees.FirstOrDefault(e =>
            !string.IsNullOrWhiteSpace(e.SignInId)
            && e.SignInId.Trim().ToLowerInvariant() == lower
            && e.UniqueId != SeedUniqueId);
        if (signInConflict is not null)
        {
            if (SyncSeedRow(signInConflict, signInId, pin))
                db.SaveChanges();
            return;
        }

        db.Employees.Add(new Employee
        {
            UniqueId = SeedUniqueId,
            SignInId = signInId,
            Name = "Web Admin (settings)",
            Role = "AdminWeb",
            PinCode = EmployeePinHasher.HashForStorage(pin),
            ProfileImagePath = string.Empty,
            PhoneNumber = string.Empty,
            HourlyRate = 0m,
            MonthlySalaryUSD = 0m,
            JoinDate = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc),
            EmploymentStatus = "Active",
            Notes = "Read-only admin web account; credentials from business settings.",
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

    private static bool SyncSeedRow(Employee employee, string signInId, string pin)
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

        if (!string.Equals(employee.SignInId?.Trim(), signInId, StringComparison.OrdinalIgnoreCase))
        {
            employee.SignInId = signInId;
            changed = true;
        }

        if (!EmployeePinHasher.Verify(pin, employee.PinCode))
        {
            employee.PinCode = EmployeePinHasher.HashForStorage(pin);
            changed = true;
        }

        var notes = employee.Notes ?? string.Empty;
        if (!notes.Contains("admin web", StringComparison.OrdinalIgnoreCase))
        {
            employee.Notes = "Read-only admin web account; credentials from business settings.";
            changed = true;
        }

        return changed;
    }
}
