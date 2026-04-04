namespace EliteRestaurantPro.Models;

public class OrderRecord
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
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
    public decimal CustomerPaidUsd { get; set; }
    public decimal CustomerPaidFc { get; set; }
    public decimal ChangeGivenUsd { get; set; }
    public decimal ChangeGivenFc { get; set; }
    public decimal ExchangeRateUsed { get; set; } = 2250m;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>When the order was marked Completed (payment recorded). Used for money ledger date.</summary>
    public DateTime? CompletedAt { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
