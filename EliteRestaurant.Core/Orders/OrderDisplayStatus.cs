using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Orders;

/// <summary>UI-facing order status; overrides workflow status when client debt is still open.</summary>
public static class OrderDisplayStatus
{
    public const string Debt = "Debt";
    public const string Refunded = "Refunded";
    private const decimal SettledToleranceUsd = 0.01m;

    public static bool HasOpenOnAccountDebt(OrderRecord order) =>
        ClientSettlement.IsOnAccount(order.ClientSettlement)
        && order.ClientDebtSettledUsd < order.AmountOnAccountUsd - SettledToleranceUsd;

    public static bool IsRefundedCompleted(OrderRecord order) =>
        string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase)
        && order.RefundedAtUtc.HasValue;

    public static string ForOrder(OrderRecord order)
    {
        if (IsRefundedCompleted(order))
            return Refunded;
        return HasOpenOnAccountDebt(order) ? Debt : (order.Status ?? string.Empty);
    }

    public static string ForOrder(
        string workflowStatus,
        string? clientSettlement,
        decimal amountOnAccountUsd,
        decimal clientDebtSettledUsd) =>
        ClientSettlement.IsOnAccount(clientSettlement)
        && clientDebtSettledUsd < amountOnAccountUsd - SettledToleranceUsd
            ? Debt
            : (workflowStatus ?? string.Empty);
}
