using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>
/// Kitchen-facing text derived from <see cref="OrderRecord.CustomerNotes"/> for online orders encoded by
/// <c>PublicMenuController</c> (middot-separated segments). Excludes guest contact, channel, address, delivery
/// instructions, and payment labels — kitchen only sees unlabeled segments (public menu <c>body.Notes</c>).
/// </summary>
public static class KitchenCustomerNotesDisplay
{
    private const string NoKitchenCustomerNotes = "\u2014";

    /// <summary>
    /// For online orders, returns only unlabeled customer segments (e.g. public menu &quot;Notes&quot;), never structured
    /// <c>Instructions:</c> delivery text. For non-online orders, returns trimmed <see cref="OrderRecord.CustomerNotes"/> unchanged.
    /// </summary>
    public static string ForKitchen(OrderRecord order)
    {
        var raw = (order.CustomerNotes ?? string.Empty).Trim();
        if (raw.Length == 0)
            return OrderOrigin.IsOnline(order.OrderOrigin) ? NoKitchenCustomerNotes : string.Empty;

        if (!OrderOrigin.IsOnline(order.OrderOrigin))
            return raw;

        var freeSegments = new List<string>();
        var parts = OnlineOrderCustomerNotes.EnumerateStructuredNoteParts(raw).ToArray();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
                continue;

            if (TrySkipOnlineChannelParts(parts, ref i))
                continue;

            if (IsOnlineChannelSegment(part))
                continue;
            if (StartsWithInsensitive(part, "Guest:"))
                continue;
            if (StartsWithInsensitive(part, "Phone:"))
                continue;
            if (StartsWithInsensitive(part, "Address:"))
                continue;
            if (StartsWithInsensitive(part, "Pay:"))
                continue;
            if (StartsWithInsensitive(part, "Instructions:"))
                continue;

            var free = OnlineOrderCustomerNotes.UnescapeField(part.Trim());
            if (free.Length > 0)
                freeSegments.Add(free);
        }

        if (freeSegments.Count > 0)
        {
            var joined = string.Join(
                " · ",
                freeSegments.Select(s => s.Trim()).Where(s => s.Length > 0));
            if (joined.Length > 0)
                return joined;
        }

        if (LooksLikeOnlineGuestBlock(raw))
            return NoKitchenCustomerNotes;

        return raw;
    }

    /// <summary>
    /// <c>PublicMenuController</c> stores channel as two middot-separated tokens (<c>Online</c> then <c>Pickup</c>/<c>Delivery</c>).
    /// </summary>
    private static bool TrySkipOnlineChannelParts(string[] parts, ref int i)
    {
        var t = parts[i].Trim();
        if (!t.Equals("Online", StringComparison.OrdinalIgnoreCase) || i + 1 >= parts.Length)
            return false;
        var next = parts[i + 1].Trim();
        if (!next.Equals("Pickup", StringComparison.OrdinalIgnoreCase)
            && !next.Equals("Delivery", StringComparison.OrdinalIgnoreCase))
            return false;
        i++;
        return true;
    }

    private static bool LooksLikeOnlineGuestBlock(string raw) =>
        raw.Contains("Guest:", StringComparison.OrdinalIgnoreCase)
        && raw.Contains("Pay:", StringComparison.OrdinalIgnoreCase);

    private static bool IsOnlineChannelSegment(string part)
    {
        var n = part.Trim();
        return n.Equals("Online · Pickup", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Online · Delivery", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithInsensitive(string part, string prefix) =>
        part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
