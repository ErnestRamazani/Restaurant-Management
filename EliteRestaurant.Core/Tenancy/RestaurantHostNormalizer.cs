namespace EliteRestaurant.Core.Tenancy;

public static class RestaurantHostNormalizer
{
    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        var h = host.Trim().ToLowerInvariant();
        var port = h.IndexOf(':');
        if (port > 0)
            h = h[..port];

        if (h.StartsWith("www.", StringComparison.Ordinal))
            h = h[4..];

        return h;
    }
}
