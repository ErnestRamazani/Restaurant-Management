using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Api.Orders;

public static class CashierOrderDetailBuilder
{
    public static CashierOrderDetailDto Build(OrderRecord order, RestaurantClient? client = null, decimal debtCapUsd = 250m)
    {
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var merchTotals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(
            lineSubtotal,
            order.DiscountMode,
            order.DiscountValue,
            order.DeliveryFeeUsd);
        var lines = order.Items.Select(i => new CashierOrderLineDto(
            i.ProductId,
            i.Product?.Name ?? "Unknown",
            i.Product?.Price ?? 0m,
            i.Quantity,
            (i.Product?.Price ?? 0m) * i.Quantity)).ToList();

        var debtBalance = client?.DebtBalanceUsd ?? 0m;
        var canAddToDebt = client is not null
            && debtBalance < debtCapUsd
            && !ClientSettlement.IsOnAccount(order.ClientSettlement);

        return new CashierOrderDetailDto(
            order.Id,
            string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            (order.ConfirmationCode ?? string.Empty).Trim(),
            OrderRecordUiLabels.TableCaption(order),
            OrderRecordUiLabels.ServerCaption(order),
            OrderDisplayStatus.ForOrder(order),
            order.CustomerNotes ?? string.Empty,
            order.AllergyNotes ?? string.Empty,
            order.DiscountMode ?? "None",
            order.DiscountValue,
            lineSubtotal,
            merchTotals.Tax,
            merchTotals.Service,
            merchTotals.DiscountApplied,
            totals.GrandTotal,
            CurrencyHelper.ConvertUsdToFc(totals.GrandTotal),
            lines,
            string.IsNullOrWhiteSpace(order.OrderOrigin) ? OrderOrigin.InStore : order.OrderOrigin,
            order.OrderSource ?? "WalkIn",
            order.DeliveryFeeUsd,
            string.IsNullOrWhiteSpace(order.PaymentTiming) ? OrderPaymentTiming.Immediate : order.PaymentTiming,
            merchTotals.TaxableSubtotal,
            merchTotals.GrandTotal,
            order.RestaurantClientId,
            client?.FullName,
            debtBalance,
            canAddToDebt);
    }
}
