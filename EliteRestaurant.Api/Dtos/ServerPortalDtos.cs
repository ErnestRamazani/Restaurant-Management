namespace EliteRestaurant.Api.Dtos;

public sealed record ServerProductDto(
    int Id,
    string UniqueId,
    string Name,
    string Category,
    string SubCategory,
    decimal Price,
    bool InStock,
    string? PhotoUrl,
    string? Description,
    string? Composition,
    int PrepMinutes);

public sealed record ServerOrderLineRequest(int ProductId, int Quantity);

public sealed record ServerCreateOrderRequest(
    int TableId,
    string OrderSource,
    string SourceReference,
    string DiscountMode,
    decimal DiscountValue,
    string PaymentCurrencyCode,
    bool AppendToOpenCheck,
    int? OpenOrderId,
    string? NewCheckKind,
    string CustomerNotes,
    string AllergyNotes,
    IReadOnlyList<ServerOrderLineRequest> Lines);

public sealed record ServerOpenCheckLineDto(
    int ProductId,
    string Name,
    string Category,
    int Quantity,
    decimal LineTotalUsd);

public sealed record ServerOpenCheckDto(
    int OrderId,
    string OrderCode,
    string Status,
    string CheckKind,
    DateTime CreatedAt,
    string CustomerNotes,
    string AllergyNotes,
    decimal SubtotalUsd,
    decimal GrandTotalUsd,
    IReadOnlyList<ServerOpenCheckLineDto> Lines);

public sealed record ServerOpenChecksResponse(
    int TableId,
    IReadOnlyList<ServerOpenCheckDto> Checks);

public sealed record ServerCreateOrderResponse(
    string Mode,
    string OrderId,
    int LinesAdded,
    string Message,
    DateTime CreatedAtUtc);

public sealed record ServerPortalConfigDto(
    string RestaurantName,
    string RestaurantLogoUrl,
    string EmployeePhotoUrl,
    string CurrencyDisplayMode,
    decimal UsdToFcRate,
    decimal TaxPercent,
    decimal ServicePercent);

public sealed record ServerReadyOrderLineDto(
    int ProductId,
    string Name,
    int Quantity,
    string? PhotoUrl);

public sealed record ServerReadyOrderDto(
    int Id,
    string OrderId,
    int TableId,
    string TableLabel,
    string ServerName,
    string Status,
    string ItemsSummary,
    int ItemCount,
    decimal TotalUsd,
    decimal TotalFc,
    DateTime CreatedAt,
    string TimeText,
    string CustomerNotes,
    string AllergyNotes,
    string? GuestCustomerName,
    string OrderOrigin,
    string OrderSource,
    bool IsOnlineMenuOrder,
    IReadOnlyList<ServerReadyOrderLineDto> Lines);

public sealed record ServerMarkServedResponse(
    bool Ok,
    string Message,
    string OrderId,
    string NewStatus,
    DateTime UpdatedAtUtc);

public sealed record ServerDraftDto(
    string Id,
    string Label,
    string SnapshotJson,
    DateTime UpdatedAtUtc,
    int TableId,
    bool IsCustomerDraft);

public sealed record ServerSaveDraftRequest(
    string Label,
    string SnapshotJson);
