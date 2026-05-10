using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Ensures the documented default read-only web admin row exists after migrations.
/// (If the seed migration was skipped or the DB predates it, login would otherwise always fail.)
/// </summary>
public static class AdminWebLoginSeed
{
    public const string SeedUniqueId = "EMP-SEED-ADMINWEB";
    public const string SeedSignInId = "er4124";
    public const string DefaultPinPlaintext = "4124";

    public static void EnsureSeeded(AppDbContext db)
    {
        if (db.Employees.Any(e => e.UniqueId == SeedUniqueId))
            return;

        var lower = SeedSignInId.ToLowerInvariant();
        if (db.Employees.Any(e =>
                !string.IsNullOrWhiteSpace(e.SignInId)
                && e.SignInId.Trim().ToLowerInvariant() == lower))
            return;

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
}
