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

        foreach (var part in OnlineOrderCustomerNotes.EnumerateStructuredNoteParts(order.CustomerNotes))
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

    private static bool TryExtract(string part, string prefix, ref string target)
    {
        if (!part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        target = OnlineOrderCustomerNotes.UnescapeField(part[prefix.Length..].Trim());
        return true;
    }
}
