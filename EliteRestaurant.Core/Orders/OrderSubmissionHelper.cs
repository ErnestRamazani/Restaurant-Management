using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Orders;

/// <summary>Shared order line assignment, payment sync, and reservation linking (desktop + reusable).</summary>
public static class OrderSubmissionHelper
{
    public static (int? EmployeeId, string Role, string Name) ResolveAssignee(
        IReadOnlyDictionary<int, Product> productById,
        IReadOnlyList<Employee> activeStaff,
        int productId)
    {
        if (!productById.TryGetValue(productId, out var product))
            return (null, "Unknown", "Unassigned");

        if (string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase))
        {
            var barman = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
            return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
        }

        var chef = activeStaff.FirstOrDefault(e => e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
        return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
    }

    public static void SyncPaymentFields(OrderRecord order, AppDbContext db)
    {
        var items = order.Items.ToList();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var prices = db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Price);
        ApplyComputedPaymentAmounts(order, items, prices);
    }

    /// <summary>Tablet/API path: subtotal from preloaded product map; clears customer payment fields for a fresh ticket.</summary>
    public static void SyncPaymentFields(OrderRecord order, IReadOnlyDictionary<int, Product> products)
    {
        var items = order.Items.ToList();
        var prices = items.Select(i => i.ProductId).Distinct()
            .ToDictionary(id => id, id => products.TryGetValue(id, out var p) ? p.Price : 0m);
        ApplyComputedPaymentAmounts(order, items, prices);
        order.CustomerPaidUsd = 0m;
        order.CustomerPaidFc = 0m;
        order.ChangeGivenUsd = 0m;
        order.ChangeGivenFc = 0m;
        order.ExchangeRateUsed = CurrencyHelper.FcPerUsd;
    }

    private static void ApplyComputedPaymentAmounts(
        OrderRecord order,
        List<OrderItem> items,
        IReadOnlyDictionary<int, decimal> priceByProductId)
    {
        var subtotal = items.Sum(i => (priceByProductId.TryGetValue(i.ProductId, out var price) ? price : 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotalsWithDeliveryFee(subtotal, order.DiscountMode, order.DiscountValue, order.DeliveryFeeUsd);
        var grand = totals.GrandTotal;
        order.DiscountAmountUsd = totals.DiscountApplied;
        order.PaymentAmountUsd = Math.Round(grand, 2);
        order.PaymentAmountFc = CurrencyHelper.ConvertUsdToFc(grand);
        order.PaymentAmount = string.Equals(order.PaymentCurrencyCode, CurrencyHelper.CongoleseFranc, StringComparison.OrdinalIgnoreCase)
            ? order.PaymentAmountFc
            : order.PaymentAmountUsd;
    }

    public static void ApplyReservationLink(
        OrderRecord order,
        AppDbContext db,
        string selectedOrderSource,
        string sourceReference,
        string reservationCode,
        string? reservationGuestName)
    {
        if (string.Equals(selectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            order.OrderSource = "Delivery";
            order.ReservationGuestName = sourceReference;
            return;
        }

        if (string.Equals(selectedOrderSource, "WalkIn", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(reservationCode))
        {
            order.OrderSource = "WalkIn";
            return;
        }

        order.OrderSource = "Reservation";
        order.ReservationCode = reservationCode.Trim();
        order.ReservationGuestName = reservationGuestName?.Trim() ?? string.Empty;

        var reservation = db.Reservations.SingleOrDefault(r => r.UniqueId == order.ReservationCode);
        if (reservation is null)
            return;

        order.ReservationBookingId = reservation.Id;
        if (string.IsNullOrWhiteSpace(order.ReservationGuestName))
            order.ReservationGuestName = reservation.GuestName;
        if (reservation.TableId.HasValue && !order.TableId.HasValue)
            order.TableId = reservation.TableId;

        if (string.Equals(reservation.Status, "Arrived", StringComparison.OrdinalIgnoreCase))
        {
            reservation.Status = "Completed";
            reservation.UpdatedAt = DateTime.Now;
        }
    }
}
