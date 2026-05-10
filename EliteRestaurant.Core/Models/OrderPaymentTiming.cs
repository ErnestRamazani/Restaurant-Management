namespace EliteRestaurant.Core.Models;

/// <summary>Persisted on <see cref="OrderRecord.PaymentTiming"/>. Deferred orders post to Money only after cashier payment confirmation.</summary>
public static class OrderPaymentTiming
{
    public const string Immediate = "Immediate";
    public const string Deferred = "Deferred";

    public static bool IsDeferred(string? value) =>
        string.Equals(value, Deferred, StringComparison.OrdinalIgnoreCase);
}
