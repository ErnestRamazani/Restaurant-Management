namespace EliteRestaurantPro.Services;

public sealed record CreateOrderOpenCheckInfo(int? OrderId, string Code, string Status);

public sealed record CreateOrderPhaseResult(
    bool Ok,
    string Caption,
    string Message,
    int TableNumber,
    string TableName,
    CreateOrderOpenCheckInfo OpenCheck);

public sealed record CreateOrderAppendResult(bool Ok, string Caption, string Message);

public sealed record CreateOrderSaveResult(bool Ok, string Caption, string Message);

public sealed record CreateOrderSubmitSnapshot(
    int TableId,
    IReadOnlyList<(int ProductId, int Quantity)> SelectedLines,
    string CustomerNotes,
    string AllergyNotes,
    string DiscountMode,
    string DiscountInput,
    decimal LiveDiscountAmount,
    decimal LiveSubtotal,
    decimal LiveGrandTotal,
    decimal LiveGrandTotalFc,
    string LiveDiscountLabel,
    string LiveGrandTotalUsdText,
    string LiveGrandTotalFcText,
    string SelectedPaymentCurrency,
    string ChosenPaymentAmountText,
    string EstimatedPrepText,
    string SelectedOrderStatus,
    string SelectedOrderSource,
    string SourceReference,
    bool IsTabletStaffOrderFlow,
    int? ServerEmployeeId,
    string ServerEmployeeName,
    string ReservationCode,
    string ReservationGuestName,
    /// <summary>Optional: Immediate or Deferred.</summary>
    string? PaymentTiming);
