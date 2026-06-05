namespace EliteRestaurant.Api.Dtos;

/// <summary>Realtime order pipeline event for staff portals (toast + ring).</summary>
public sealed class OrderStageChangedDto
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string? PreviousStatus { get; init; }
    public string NewStatus { get; init; } = string.Empty;
    /// <summary>Machine key, e.g. <c>pending-cashier</c>, <c>released-to-kitchen</c>, <c>status-ready</c>.</summary>
    public string Stage { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    /// <summary>Hub groups that should alert: Server, Cashier, Kitchen, Reception.</summary>
    public IReadOnlyList<string> Audiences { get; init; } = Array.Empty<string>();
}
