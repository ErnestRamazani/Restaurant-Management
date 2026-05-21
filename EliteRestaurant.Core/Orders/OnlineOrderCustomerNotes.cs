using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>Encoding for multi-line guest fields embedded in <see cref="Models.OrderRecord.CustomerNotes"/>.</summary>
public static class OnlineOrderCustomerNotes
{
    /// <summary>Middot-like glyphs used between online note segments (see <c>PublicMenuController</c> join).</summary>
    public static readonly char[] StructuredNoteSeparators = ['\u00B7', '\u2219', '\u2022'];

    /// <summary>
    /// Splits persisted online <see cref="OrderRecord.CustomerNotes"/> blobs the same way as
    /// <see cref="DeliveryTicketInfoParser"/> (newline, then middot segments).
    /// </summary>
    public static IEnumerable<string> EnumerateStructuredNoteParts(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            yield break;

        foreach (var line in notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var part in line.Split(StructuredNoteSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                    yield return part;
            }
        }
    }

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
