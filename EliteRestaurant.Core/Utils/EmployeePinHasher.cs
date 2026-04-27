namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Stores employee PINs as BCrypt hashes in <see cref="Models.Employee.PinCode"/>.
/// Legacy plaintext values (pre-migration) are still accepted by <see cref="Verify"/> until re-saved.
/// </summary>
public static class EmployeePinHasher
{
    public static string HashForStorage(string plainPin)
    {
        var p = (plainPin ?? string.Empty).Trim();
        if (p.Length == 0)
            throw new ArgumentException("PIN cannot be empty.", nameof(plainPin));
        return BCrypt.Net.BCrypt.HashPassword(p);
    }

    /// <summary>True if <paramref name="plainPin"/> matches the stored BCrypt hash or legacy plaintext.</summary>
    public static bool Verify(string plainPin, string? storedHashOrLegacy)
    {
        if (string.IsNullOrWhiteSpace(plainPin) || string.IsNullOrWhiteSpace(storedHashOrLegacy))
            return false;
        var pin = plainPin.Trim();
        var stored = storedHashOrLegacy.Trim();
        if (LooksLikeBcryptHash(stored))
            return BCrypt.Net.BCrypt.Verify(pin, stored);
        return string.Equals(pin, stored, StringComparison.Ordinal);
    }

    public static bool LooksLikeBcryptHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 59)
            return false;
        return value.StartsWith("$2", StringComparison.Ordinal);
    }
}
