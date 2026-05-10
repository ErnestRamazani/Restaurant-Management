namespace EliteRestaurant.Core.Utils;

/// <summary>Kitchen sees only kitchen-queue statuses; in-store tablet flow uses <see cref="PendingCashier"/>; public online uses <see cref="PendingApproval"/>.</summary>
public static class OrderWorkflow
{
    public const string PendingCashier = "Pending cashier";
    /// <summary>Online-submitted orders await cashier approval before inventory deduction and kitchen queue.</summary>
    public const string PendingApproval = "Pending approval";

    public static bool IsPendingCashier(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        return string.Equals(status.Trim(), PendingCashier, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPendingApproval(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        return string.Equals(status.Trim(), PendingApproval, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AwaitsCashierOrApprovalBeforeKitchen(string? status) =>
        IsPendingCashier(status) || IsPendingApproval(status);

    /// <summary>
    /// Active line after cashier release: <c>Waiting</c> (kitchen should receive),
    /// <c>In Kitchen</c> (preparing), <c>Ready</c> (pickup). Kitchen tablet moves Waiting→In Kitchen→Ready.
    /// </summary>
    public static bool IsKitchenQueueStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        var s = status.Trim();
        return string.Equals(s, "Waiting", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "In Kitchen", StringComparison.OrdinalIgnoreCase)
               || string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>After pickup from kitchen; guest is being served — cashier may complete only from this state.</summary>
    public const string Served = "Served";

    public static bool IsServed(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        string.Equals(status.Trim(), Served, StringComparison.OrdinalIgnoreCase);

    /// <summary>Cashier/admin may record payment only when the server has confirmed served.</summary>
    public static bool CanCashierComplete(string? status) => IsServed(status);

    /// <summary>
    /// Admin-only manual steps on the Orders screen (kitchen tablet still owns Waiting → In Kitchen → Ready).
    /// Includes Ready → Served so an admin-run order can continue without a server tablet.
    /// </summary>
    public static bool CanAdminAdvanceOrderStatus(string? status) =>
        string.Equals(status, "Waiting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "In Kitchen", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase);

    /// <summary>Table stays occupied for in-service, unvalidated server tickets, and until payment (Served).</summary>
    public static bool OccupiesTable(string? status) =>
        IsKitchenQueueStatus(status) || IsPendingCashier(status) || IsPendingApproval(status) || IsServed(status);

    /// <summary>One open check per table until completed or cancelled — can add more lines to the same ticket.</summary>
    /// <remarks>Do not use this inside EF IQueryable filters (not translatable). Use WhereOpenCheckForTable in OrderRecordQueryExtensions for database queries.</remarks>
    public static bool IsOpenCheckStatus(string? status) =>
        IsPendingCashier(status) || IsPendingApproval(status) || IsKitchenQueueStatus(status) || IsServed(status);
}
