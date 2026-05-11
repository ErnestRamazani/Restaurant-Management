using System.Text.Json.Serialization;

namespace EliteRestaurant.Api.Dtos;

public sealed record PublicMenuConfigDto(
    string RestaurantName,
    string? LogoUrl,
    string? Tagline,
    string DefaultCurrencyMode,
    decimal UsdToFcRate,
    decimal TaxPercent,
    decimal ServicePercent,
    string? Phone,
    string? Address,
    string? WebsiteDomain,
    string? SocialMedia,
    string? TicketFooterText,
    string? TaxIdLegalInfo,
    int? OnlineOrdersTableId,
    string? OnlinePromoTitle,
    string? OnlinePromoSubtitle,
    string? OnlinePromoCtaLabel,
    string? OnlinePromoImageUrl);

public sealed record StaffLoginCodeRequest(string? Code, string? SignInId = null, string? Pin = null);

public sealed record StaffLoginCodeResponse(
    bool Success,
    string? Message = null,
    string? AccessToken = null,
    DateTime? ExpiresAtUtc = null);

public sealed class PublicProductDto
{
    public int Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    [JsonPropertyName("subcategory")]
    public string Subcategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? Composition { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class PublicTableDto
{
    public int Id { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    /// <summary>Assigned server display name from the back office, when set.</summary>
    [JsonPropertyName("assignedServerName")]
    public string? AssignedServerName { get; set; }
}

public sealed class CustomerDraftItemRequest
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class CustomerDraftRequest
{
    public int TableId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<CustomerDraftItemRequest> Items { get; set; } = new();
    public string? Notes { get; set; }
    public string? AllergyNotes { get; set; }
    public string? CookingTimeNote { get; set; }
    /// <summary><c>food</c> or <c>drink</c> — used for draft list label and server portal display.</summary>
    public string? OrderKind { get; set; }
}

public sealed class PublicMenuDraftSuccessDto
{
    public bool Success { get; set; } = true;
    public string Label { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Estimated prep (minutes), same model as the POS and WPF.</summary>
    public int EstimatedPrepMinutes { get; set; }
}

public sealed class PublicMenuDraftErrorDto
{
    public bool Success { get; set; } = false;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}

/// <summary>Anonymous guest polling for order status (rate-limited).</summary>
public sealed class PublicOrderStatusDto
{
    public string OrderCode { get; set; } = string.Empty;
    public string WorkflowStatus { get; set; } = string.Empty;
    /// <summary>Machine code when the kitchen has marked ready (ReadyForPickup / OutForDelivery).</summary>
    public string? CustomerFulfillmentStatus { get; set; }
    public string? CustomerFulfillmentDisplay { get; set; }
    public string? TableLabel { get; set; }
}
