using EliteRestaurant.Contracts.Admin;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminOrdersApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public async Task<AdminCreateOrderResponse> CreateOrderAsync(
        CreateOrderSubmitSnapshot snapshot,
        bool appendToOpenCheck,
        int? openOrderId,
        CancellationToken cancellationToken = default)
    {
        var request = new AdminCreateOrderRequest(
            snapshot.TableId,
            snapshot.ServerEmployeeId,
            snapshot.ServerEmployeeName,
            snapshot.SelectedOrderSource,
            snapshot.SourceReference,
            snapshot.ReservationCode,
            snapshot.ReservationGuestName,
            snapshot.SelectedOrderStatus,
            snapshot.IsTabletStaffOrderFlow,
            appendToOpenCheck,
            openOrderId,
            snapshot.DiscountMode,
            snapshot.DiscountInput,
            snapshot.SelectedPaymentCurrency,
            snapshot.LiveGrandTotal,
            snapshot.LiveGrandTotalFc,
            snapshot.LiveDiscountAmount,
            snapshot.CustomerNotes,
            snapshot.AllergyNotes,
            snapshot.PaymentTiming,
            snapshot.SelectedLines
                .Select(line => new AdminOrderLineRequest(line.ProductId, line.Quantity))
                .ToList());

        return await _apiClient.PostAsync<AdminCreateOrderRequest, AdminCreateOrderResponse>(
                   "api/admin/orders/create",
                   request,
                   cancellationToken)
               ?? new AdminCreateOrderResponse(false, "Create Order", "Cloud API returned an empty response.", null);
    }

    public Task<AdminOrderReleasePendingResponse?> ReleasePendingToKitchenAsync(int orderId, CancellationToken cancellationToken = default) =>
        _apiClient.PostAsync<object, AdminOrderReleasePendingResponse>(
            $"api/admin/orders/pending/{orderId}/release-to-kitchen",
            new { },
            cancellationToken);

    public Task<AdminOrderOpMessageResponse?> CancelPendingAsync(int orderId, CancellationToken cancellationToken = default) =>
        _apiClient.PostAsync<object, AdminOrderOpMessageResponse>(
            $"api/admin/orders/pending/{orderId}/cancel",
            new { },
            cancellationToken);

    public Task<AdminOrderOpMessageResponse?> CreateWalkInFromDeskAsync(
        AdminWalkInOrderDeskRequest request,
        CancellationToken cancellationToken = default) =>
        _apiClient.PostAsync<AdminWalkInOrderDeskRequest, AdminOrderOpMessageResponse>(
            "api/admin/orders/walk-in",
            request,
            cancellationToken);

    public Task<AdminOrderAdvanceResponse?> AdvanceOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        _apiClient.PostAsync<object, AdminOrderAdvanceResponse>(
            $"api/admin/orders/{orderId}/advance",
            new { },
            cancellationToken);

    public Task<AdminOrderOpMessageResponse?> UpdateOrderStatusAsync(
        int orderId,
        AdminOrderStatusUpdateRequest request,
        CancellationToken cancellationToken = default) =>
        _apiClient.PostAsync<AdminOrderStatusUpdateRequest, AdminOrderOpMessageResponse>(
            $"api/admin/orders/{orderId}/status",
            request,
            cancellationToken);
}
