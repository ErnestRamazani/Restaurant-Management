namespace EliteRestaurant.Api.Dtos;

public sealed record CashierPendingOrderDto(
    int Id,
    string OrderCode,
    string TableLabel,
    string ServerName,
    DateTime CreatedAt,
    string CreatedAtText,
    decimal GrandTotalUsd,
    string GrandTotalText,
    string LinesSummary,
    string Status,
    string OrderOrigin);

public sealed record CashierCompleteOrderRequest(
    string? PaymentCurrencyCode,
    decimal PaidUsd,
    decimal PaidFc,
    decimal ChangeUsd,
    decimal ChangeFc);

public sealed record CashierOrderDetailDto(
    int Id,
    string OrderCode,
    string TableLabel,
    string ServerName,
    string Status,
    string CustomerNotes,
    string AllergyNotes,
    string DiscountMode,
    decimal DiscountValue,
    decimal SubtotalUsd,
    decimal TaxUsd,
    decimal ServiceUsd,
    decimal DiscountAppliedUsd,
    decimal GrandTotalUsd,
    decimal GrandTotalFc,
    IReadOnlyList<CashierOrderLineDto> Lines,
    string OrderOrigin,
    string OrderSource,
    decimal DeliveryFeeUsd,
    string PaymentTiming,
    decimal TaxableSubtotalUsd,
    decimal MerchandiseGrandUsd);

public sealed record CashierOrderLineDto(
    int ProductId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
