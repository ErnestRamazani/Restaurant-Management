namespace EliteRestaurant.Core.Utils;

/// <summary>Customer-visible fulfillment phase once the kitchen finishes prep (online / remote orders).</summary>
public static class CustomerFulfillmentStatuses
{
    public const string ReadyForPickup = "ReadyForPickup";
    public const string OutForDelivery = "OutForDelivery";

    /// <summary>Default: pick-up unless the order is tagged as delivery.</summary>
    public static string ResolveCodeForOrder(string? orderSource) =>
        string.Equals(orderSource?.Trim(), "Delivery", StringComparison.OrdinalIgnoreCase)
            ? OutForDelivery
            : ReadyForPickup;

    public static string ToDisplay(string? code) => code switch
    {
        OutForDelivery => "Out for delivery",
        ReadyForPickup => "Ready for pick-up",
        _ => string.Empty
    };
}
