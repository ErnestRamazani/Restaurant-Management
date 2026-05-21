using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Orders;

/// <summary>Kitchen KDS payload: order header plus per-line kitchen visibility flags.</summary>
public static class KitchenOrderQueueMapper
{
    public static KitchenOrderQueueRow ToQueueRow(OrderRecord order, string? prepStationPortal = null)
    {
        var allItems = order.Items?.ToList() ?? [];
        var items = KitchenQueueKindFilter.FilterItemsForPortal(prepStationPortal, allItems);
        var work = KitchenLineVisibility.Summarize(items);
        var lines = items.Select(i => new KitchenOrderLineRow(
            i.Id,
            i.ProductId,
            i.Quantity,
            i.PreparedByRole,
            i.PreparedByName,
            i.PreparedByEmployeeId,
            i.KitchenPreparedAt,
            KitchenLineVisibility.IsNewForKitchen(i, allItems),
            KitchenLineVisibility.KitchenLineStatus(i))).ToList();

        var checkKind = KitchenQueueKindFilter.TryInferOrderCheckKind(order)
            ?? OpenCheckKindHelper.Food;

        return new KitchenOrderQueueRow(
            order.Id,
            order.UniqueId,
            order.ConfirmationCode,
            order.TableId,
            order.TableCode,
            order.TableName,
            order.ServerId,
            order.ServerName,
            order.Status,
            order.CustomerNotes,
            KitchenCustomerNotesDisplay.ForKitchen(order),
            order.AllergyNotes,
            order.DiscountMode,
            order.DiscountValue,
            order.PaymentCurrencyCode,
            order.OrderSource,
            order.OrderOrigin,
            order.ReservationGuestName,
            order.CustomerFulfillmentStatus,
            order.CreatedAt,
            checkKind,
            work.CardSummaryText,
            work.NewCount,
            work.PreparedCount,
            lines);
    }
}

public sealed record KitchenOrderQueueRow(
    int Id,
    string UniqueId,
    string? ConfirmationCode,
    int? TableId,
    string TableCode,
    string TableName,
    int? ServerId,
    string ServerName,
    string Status,
    string CustomerNotes,
    string KitchenCustomerNotes,
    string AllergyNotes,
    string DiscountMode,
    decimal DiscountValue,
    string PaymentCurrencyCode,
    string OrderSource,
    string OrderOrigin,
    string ReservationGuestName,
    string? CustomerFulfillmentStatus,
    DateTime CreatedAt,
    string CheckKind,
    string KitchenWorkSummary,
    int KitchenNewLineCount,
    int KitchenPreparedLineCount,
    IReadOnlyList<KitchenOrderLineRow> Items);

public sealed record KitchenOrderLineRow(
    int Id,
    int ProductId,
    int Quantity,
    string PreparedByRole,
    string PreparedByName,
    int? PreparedByEmployeeId,
    DateTime? KitchenPreparedAt,
    bool IsNewForKitchen,
    string KitchenLineStatus);
