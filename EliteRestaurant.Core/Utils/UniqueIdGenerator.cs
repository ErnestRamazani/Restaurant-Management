namespace EliteRestaurant.Core.Utils;

public static class UniqueIdGenerator
{
    public static string NewId(string prefix)
    {
        var token = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        return $"{prefix}-{token}";
    }
}
