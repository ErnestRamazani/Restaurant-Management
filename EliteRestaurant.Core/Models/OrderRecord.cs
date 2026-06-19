using EliteRestaurant.Core.Tenancy;

namespace EliteRestaurant.Core.Models;

public class OrderRecord : IRestaurantScoped
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    /// <summary>Guest-facing 6-digit proof code for online pickup/delivery orders.</summary>
    public string? ConfirmationCode { get; set; }
    public int? TableId { get; set; }
    public Table? Table { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public int? ServerId { get; set; }
    public Employee? Server { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string Status { get; set; } = "Waiting";
    public string CustomerNotes { get; set; } = string.Empty;
    public string AllergyNotes { get; set; } = string.Empty;
    /// <summary>None, Percent, or Usd (fixed USD off merchandise subtotal).</summary>
    public string DiscountMode { get; set; } = "None";
    /// <summary>Percent 0–100, or USD amount when mode is Usd.</summary>
    public decimal DiscountValue { get; set; }
    /// <summary>Actual discount applied to line subtotal (USD).</summary>
    public decimal DiscountAmountUsd { get; set; }
    public string PaymentCurrencyCode { get; set; } = "USD";
    public decimal PaymentAmount { get; set; }
    public decimal PaymentAmountUsd { get; set; }
    public decimal PaymentAmountFc { get; set; }
    /// <summary>Merchandise + tax + service grand (USD), excluding delivery fee line.</summary>
    public decimal MerchandiseGrandTotalUsd { get; set; }
    public decimal CustomerPaidUsd { get; set; }
    public decimal CustomerPaidFc { get; set; }
    public decimal ChangeGivenUsd { get; set; }
    public decimal ChangeGivenFc { get; set; }
    public decimal ExchangeRateUsed { get; set; } = 2250m;
    public string OrderSource { get; set; } = "WalkIn";
    /// <summary><see cref="Models.OrderOrigin.Online"/> or <see cref="Models.OrderOrigin.InStore"/>.</summary>
    public string OrderOrigin { get; set; } = global::EliteRestaurant.Core.Models.OrderOrigin.InStore;
    /// <summary>20% delivery add-on (USD), stored separately from merchandise for reporting.</summary>
    public decimal DeliveryFeeUsd { get; set; }
    /// <summary><see cref="OrderPaymentTiming"/> — when <see cref="OrderPaymentTiming.Deferred"/>, ledger revenue posts only after <see cref="PaymentConfirmedAt"/>.</summary>
    public string PaymentTiming { get; set; } = OrderPaymentTiming.Immediate;
    /// <summary>Cashier (or admin payment capture) confirmation — required before auto sale revenue is posted for completed orders.</summary>
    public DateTime? PaymentConfirmedAt { get; set; }
    /// <summary>Guest payment intent on public online checkout (Cash / Card / MobileMoney).</summary>
    public string? GuestPaymentMethod { get; set; }
    /// <summary>When the kitchen marks <c>Ready</c>: customer fulfillment code for guest-facing tracking (ReadyForPickup / OutForDelivery).</summary>
    public string? CustomerFulfillmentStatus { get; set; }
    public int? ReservationBookingId { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public string ReservationGuestName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>When the order was marked Completed (payment recorded). Used for money ledger date.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Tax percent in effect when totals were computed (0 = legacy, use settings fallback).</summary>
    public decimal TaxPercentApplied { get; set; }

    /// <summary>Service percent in effect when totals were computed (0 = legacy, use settings fallback).</summary>
    public decimal ServicePercentApplied { get; set; }

    /// <summary>Set when a completed order receives a ledger refund reversal.</summary>
    public DateTime? RefundedAtUtc { get; set; }
    public int? RestaurantClientId { get; set; }
    public RestaurantClient? RestaurantClient { get; set; }
    /// <summary><see cref="Models.ClientSettlement"/> code.</summary>
    public string ClientSettlement { get; set; } = Models.ClientSettlement.None;
    /// <summary>USD grand total placed on client account when <see cref="ClientSettlement.OnAccount"/>.</summary>
    public decimal AmountOnAccountUsd { get; set; }
    /// <summary>USD portion of <see cref="AmountOnAccountUsd"/> already recognized as revenue via debt settlement.</summary>
    public decimal ClientDebtSettledUsd { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
