namespace EliteRestaurant.Core.Orders;

/// <summary>Encoding for multi-line guest fields embedded in <see cref="Models.OrderRecord.CustomerNotes"/>.</summary>
public static class OnlineOrderCustomerNotes
{
    /// <summary>Escape newlines so note parts stay on one physical line (middot-separated).</summary>
    public static string EscapeField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value.Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>Restore guest-entered line breaks after parsing a note field.</summary>
    public static string UnescapeField(string value) =>
        (value ?? string.Empty).Replace("\\n", "\n", StringComparison.Ordinal);
}
