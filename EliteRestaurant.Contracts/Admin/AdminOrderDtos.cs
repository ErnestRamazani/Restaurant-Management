namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminOrderLineRequest(int ProductId, int Quantity);

public sealed record AdminCreateOrderRequest(
    int TableId,
    int? ServerEmployeeId,
    string ServerEmployeeName,
    string SelectedOrderSource,
    string SourceReference,
    string ReservationCode,
    string ReservationGuestName,
    string SelectedOrderStatus,
    bool IsTabletStaffOrderFlow,
    bool AppendToOpenCheck,
    int? OpenOrderId,
    string DiscountMode,
    string DiscountInput,
    string SelectedPaymentCurrency,
    decimal LiveGrandTotal,
    decimal LiveGrandTotalFc,
    decimal LiveDiscountAmount,
    string CustomerNotes,
    string AllergyNotes,
    IReadOnlyList<AdminOrderLineRequest> Lines);

public sealed record AdminCreateOrderResponse(
    bool Success,
    string Title,
    string Message,
    string? OrderId);

public sealed record AdminOrderReleasePendingResponse(bool Ok, string? ErrorMessage, string? ReleasedOrderCode);

public sealed record AdminOrderOpMessageResponse(bool Ok, string? Message);

public sealed record AdminWalkInOrderDeskRequest(int TableId, string SelectedOrderStatus, IReadOnlyList<AdminOrderLineRequest> Lines);

public sealed record AdminOrderStatusUpdateRequest(
    string Status,
    string? PaymentCurrencyOverride,
    decimal PaidUsd,
    decimal PaidFc,
    decimal ChangeGivenUsd,
    decimal ChangeGivenFc);

/// <summary><c>advanced</c>, <c>missing</c> (no-op), or <c>error</c>.</summary>
public sealed record AdminOrderAdvanceResponse(string Result, string? ErrorMessage);
