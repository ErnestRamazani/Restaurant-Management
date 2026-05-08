using System.Text.Json;

namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminSyncOperationDto(
    string IdempotencyKey,
    string EntityName,
    string Operation,
    JsonElement Payload,
    DateTime QueuedAtUtc);

public sealed record AdminSyncBatchRequest(IReadOnlyList<AdminSyncOperationDto> Operations);

public sealed record AdminSyncOperationResultDto(
    string IdempotencyKey,
    string EntityName,
    string Operation,
    bool Success,
    string? Message);

public sealed record AdminSyncBatchResponse(IReadOnlyList<AdminSyncOperationResultDto> Results);

public sealed record AdminEntitySnapshotDto(
    string EntityName,
    JsonElement Payload,
    DateTime SnapshotAtUtc);

public sealed record AdminEntityListResponse(
    string EntityName,
    IReadOnlyList<JsonElement> Items,
    DateTime SnapshotAtUtc);

/// <summary>Single response for desktop Create Order — one HTTP round-trip vs four separate list calls.</summary>
public sealed record AdminCreateOrderCatalogBundleResponse(
    IReadOnlyList<JsonElement> Tables,
    IReadOnlyList<JsonElement> Products,
    IReadOnlyList<JsonElement> Reservations,
    IReadOnlyList<JsonElement> Orders,
    DateTime SnapshotAtUtc);

public sealed record AdminCloudSettingsRequest(
    string RestaurantName,
    string Phone,
    string Address,
    string WebsiteDomain,
    string SocialMedia,
    string? CustomerMenuTagline,
    string StaffLoginPasscode,
    string TicketFooterText,
    string TaxIdLegalInfo,
    string DefaultCurrencyDisplayMode,
    decimal UsdToFcRate,
    string RoundingLine,
    string RoundingSubtotal,
    string RoundingGrandTotal,
    decimal TaxPercent,
    decimal ServicePercent,
    string? LogoFileName,
    string? LogoContentType,
    string? LogoBase64);

public sealed record AdminCloudSettingsResponse(bool Success, string? LogoUrl, string Message);
