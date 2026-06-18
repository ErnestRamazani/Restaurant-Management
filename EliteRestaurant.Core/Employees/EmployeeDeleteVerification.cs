using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Staff;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Employees;

public static class EmployeeDeleteVerification
{
    public static bool IsAdminDesktopRole(string? role) =>
        StaffPortalAuthentication.IsAdminDesktopRole(role);

    public static bool RequiresAdminCredentials(string? role) =>
        IsAdminDesktopRole(role);

    public static async Task<string?> ValidateAsync(
        AppDbContext db,
        EmployeeDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Employee is null)
            return "Employee payload is required.";

        var passErr = EmployeeDeletePasscodeHelper.Validate(db, request.EmployeeDeletePasscode);
        if (passErr is not null)
            return passErr;

        var target = await ResolveTargetEmployeeAsync(db, request.Employee, cancellationToken);
        if (target is null)
            return "Employee not found.";

        if (target.Role.Equals("AdminWeb", StringComparison.OrdinalIgnoreCase))
            return "The read-only admin web account cannot be deleted from Employees. Change it in Appearance settings.";

        if (!RequiresAdminCredentials(target.Role))
            return null;

        var signInId = (request.ConfirmSignInId ?? string.Empty).Trim();
        var pin = (request.ConfirmPin ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(signInId) || string.IsNullOrEmpty(pin))
            return "Sign-in ID and PIN are required to delete an Admin or Manager account.";

        if (!CredentialsMatchEmployee(target, signInId, pin))
            return "Sign-in ID or PIN does not match this employee.";

        return null;
    }

    public static bool CredentialsMatchEmployee(Employee target, string signInId, string pin)
    {
        var lower = signInId.Trim().ToLowerInvariant();
        var idMatches =
            (!string.IsNullOrWhiteSpace(target.SignInId) && target.SignInId.Trim().ToLowerInvariant() == lower)
            || target.UniqueId.Trim().ToLowerInvariant() == lower
            || target.Name.Trim().ToLowerInvariant() == lower;

        if (!idMatches)
            return false;

        return EmployeePinHasher.Verify(pin, target.PinCode);
    }

    private static async Task<Employee?> ResolveTargetEmployeeAsync(
        AppDbContext db,
        Employee stub,
        CancellationToken cancellationToken)
    {
        if (stub.Id > 0)
        {
            var byId = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == stub.Id, cancellationToken);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(stub.UniqueId))
        {
            var uid = stub.UniqueId.Trim();
            return await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.UniqueId == uid, cancellationToken);
        }

        return null;
    }
}
