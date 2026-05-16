using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

public static class DeliveryTicketInfoParser
{
    /// <summary>Guest contact block for online pickup and delivery (parsed from <see cref="OrderRecord.CustomerNotes"/>).</summary>
    public static DeliveryTicketInfo? TryParse(OrderRecord order)
    {
        if (!OrderOrigin.IsOnline(order.OrderOrigin))
            return null;

        var name = (order.ReservationGuestName ?? string.Empty).Trim();
        var phone = string.Empty;
        var address = string.Empty;
        var instructions = string.Empty;

        foreach (var part in SplitNoteParts(order.CustomerNotes))
        {
            if (TryExtract(part, "Guest:", ref name))
                continue;
            if (TryExtract(part, "Phone:", ref phone))
                continue;
            if (TryExtract(part, "Address:", ref address))
                continue;
            if (TryExtract(part, "Instructions:", ref instructions))
                continue;
        }

        return new DeliveryTicketInfo(name, phone, address, instructions);
    }

    private static IEnumerable<string> SplitNoteParts(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            yield break;

        foreach (var line in notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var part in line.Split("·", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                    yield return part;
            }
        }
    }

    private static bool TryExtract(string part, string prefix, ref string target)
    {
        if (!part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        target = OnlineOrderCustomerNotes.UnescapeField(part[prefix.Length..].Trim());
        return true;
    }
}
