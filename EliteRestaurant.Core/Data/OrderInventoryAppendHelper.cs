using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurant.Core.Data;

/// <summary>
/// Open-check append inventory: deduct only new lines when the ticket is already in (or was in) the kitchen pipeline.
/// Pending approval (online) tickets wait for cashier release. Legacy in-store pending cashier is bulk-released on startup.
/// </summary>
public static class OrderInventoryAppendHelper
{
    public sealed record AppendValidationPlan(
        IReadOnlyList<(int ProductId, int Quantity)> LinesToValidate,
        OrderInventoryDeduction.InventoryValidationKind ValidationKind);

    /// <summary>
    /// True when append should deduct stock for <paramref name="newItems"/> now (not defer to cashier release).
    /// </summary>
    public static bool ShouldDeductNewLinesImmediately(OrderRecord order)
    {
        if (!OrderWorkflow.IsPendingCashier(order.Status) && !OrderWorkflow.IsPendingApproval(order.Status))
            return true;

        // Re-queued ticket (e.g. append while Ready): some lines were already released/deducted.
        return order.Items.Any(i => i.InventoryDeductedAt.HasValue);
    }

    public static AppendValidationPlan ResolveAppendValidation(
        OrderRecord order,
        IReadOnlyList<(int ProductId, int Quantity)> newLines)
    {
        if (ShouldDeductNewLinesImmediately(order))
            return new AppendValidationPlan(newLines, OrderInventoryDeduction.InventoryValidationKind.AdditionalLinesOnly);

        return new AppendValidationPlan(
            MergeExistingWithNewLines(order.Items, newLines),
            OrderInventoryDeduction.InventoryValidationKind.FullOrder);
    }

    /// <summary>
    /// Marks prior lines as deducted and deducts stock for new lines only. Idempotent for already-flagged lines.
    /// Caller must run inside <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransaction"/>
    /// when not using the in-memory provider.
    /// </summary>
    public static string? TryDeductNewLinesForAppend(
        AppDbContext db,
        OrderRecord order,
        IReadOnlyList<OrderItem> newItems)
    {
        if (!ShouldDeductNewLinesImmediately(order))
            return null;

        var pendingNew = newItems.Where(i => !i.InventoryDeductedAt.HasValue).ToList();
        if (pendingNew.Count == 0)
            return null;

        OrderInventoryDeduction.MarkExistingLinesAsDeducted(order, pendingNew);
        return OrderInventoryDeduction.TryApplyForAdditionalItems(db, order, pendingNew);
    }

    public static List<(int ProductId, int Quantity)> MergeExistingWithNewLines(
        IEnumerable<OrderItem> existing,
        IReadOnlyList<(int ProductId, int Quantity)> additional)
    {
        var map = existing
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var (productId, quantity) in additional)
        {
            if (!map.TryAdd(productId, quantity))
                map[productId] += quantity;
        }

        return map.Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
