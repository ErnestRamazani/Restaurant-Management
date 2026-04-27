using System.Security.Cryptography;
using System.Text;

namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Windows DPAPI (CurrentUser) for database password at rest. Prefer <c>ELITE_POSTGRES_CONNECTION</c> on non-Windows or headless services.
/// </summary>
public static class DatabaseConnectionSecret
{
    public static bool IsDpapiAvailable => OperatingSystem.IsWindows();

    public static string ProtectUtf8(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "DPAPI is Windows-only. Set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION instead.");

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string UnprotectUtf8(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return string.Empty;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "DPAPI is Windows-only. Set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION instead.");

        var protectedBytes = Convert.FromBase64String(base64.Trim());
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
