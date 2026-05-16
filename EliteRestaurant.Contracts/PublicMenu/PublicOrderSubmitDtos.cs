namespace EliteRestaurant.Contracts.PublicMenu;

public sealed record PublicOrderSubmitLine(int ProductId, int Quantity, decimal UnitPrice);

public sealed record PublicOrderSubmitRequest(
    int TableId,
    string CustomerName,
    IReadOnlyList<PublicOrderSubmitLine> Items,
    string? Notes = null,
    string? AllergyNotes = null);

public sealed record PublicOrderSubmitResponse(
    string OrderCode,
    int OrderId,
    string Status,
    string OrderOrigin,
    string? ConfirmationCode = null);

/// <summary>Guest online checkout: pickup vs delivery, mixed food+drink cart, payment intent.</summary>
public sealed record PublicOnlineOrderSubmitRequest(
    string CustomerName,
    string FulfillmentMode,
    IReadOnlyList<PublicOrderSubmitLine> Items,
    string? CustomerPhone = null,
    string? DeliveryAddress = null,
    string? DeliveryInstructions = null,
    string? PaymentMethod = null,
    string? PaymentTiming = null,
    string? Notes = null,
    string? AllergyNotes = null);
