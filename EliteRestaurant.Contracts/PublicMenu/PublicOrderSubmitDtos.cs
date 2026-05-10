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
    string OrderOrigin);
