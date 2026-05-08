using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Orders;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.Services;

/// <summary>Admin order mutations — HTTP to <c>api/admin/orders/*</c> only (single source of truth on the API host).</summary>
public sealed class AdminOrderCloudOperations
{
    private readonly AdminOrdersApiClient _ordersApi = new();

    public async Task<AdminOrderOperationsService.ReleasePendingResult> TryReleasePendingToKitchenAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var r = await _ordersApi.ReleasePendingToKitchenAsync(orderId, cancellationToken).ConfigureAwait(false)
                ?? new AdminOrderReleasePendingResponse(false, "Empty response from API.", null);
        return new AdminOrderOperationsService.ReleasePendingResult(r.Ok, r.ErrorMessage, r.ReleasedOrderCode);
    }

    public async Task<string?> TryCancelPendingCashierAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var r = await _ordersApi.CancelPendingAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (r is null)
            return "Empty response from API.";
        return r.Ok ? null : r.Message;
    }

    /// <summary>Same contract as <see cref="AdminOrderOperationsService.TryAdvanceOrder"/>: null = advanced, Empty = missing, else error.</summary>
    public async Task<string?> TryAdvanceOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var r = await _ordersApi.AdvanceOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (r is null)
            return "Empty response from API.";
        return r.Result switch
        {
            "missing" => string.Empty,
            "error" => r.ErrorMessage ?? "Advance failed.",
            "advanced" => null,
            _ => r.ErrorMessage ?? "Advance failed."
        };
    }

    public async Task UpdateOrderStatusAsync(
        int orderId,
        string status,
        string? paymentCurrencyOverride = null,
        decimal paidUsd = 0m,
        decimal paidFc = 0m,
        decimal changeGivenUsd = 0m,
        decimal changeGivenFc = 0m,
        CancellationToken cancellationToken = default)
    {
        var r = await _ordersApi.UpdateOrderStatusAsync(
            orderId,
            new AdminOrderStatusUpdateRequest(
                status,
                paymentCurrencyOverride,
                paidUsd,
                paidFc,
                changeGivenUsd,
                changeGivenFc),
            cancellationToken).ConfigureAwait(false);

        if (r is null || !r.Ok)
            throw new InvalidOperationException(r?.Message ?? "Update order status failed.");
    }

    public async Task<string?> TryCreateWalkInOrderAsync(
        int tableId,
        string selectedOrderStatus,
        IReadOnlyList<AdminWalkInLine> lines,
        CancellationToken cancellationToken = default)
    {
        var body = new AdminWalkInOrderDeskRequest(
            tableId,
            selectedOrderStatus,
            lines.Select(l => new AdminOrderLineRequest(l.ProductId, l.Quantity)).ToList());

        var r = await _ordersApi.CreateWalkInFromDeskAsync(body, cancellationToken).ConfigureAwait(false);
        if (r is null)
            return "Empty response from API.";
        return r.Ok ? null : r.Message;
    }
}
