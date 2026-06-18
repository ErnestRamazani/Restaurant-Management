using System.Text.RegularExpressions;
using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

public static class OrderWorkflow
{
    /// <summary>Online-submitted orders await cashier approval before inventory deduction and kitchen queue.</summary>
    public const string PendingApproval = "Pending approval";

    /// <summary>Legacy dine-in gate (migrated to kitchen on startup). Online orders use <see cref="PendingApproval"/>.</summary>
    public const string PendingCashier = "Pending cashier";

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

    public static bool AwaitsCashierOrApprovalBeforeKitchen(string? status)
    {
        var k = KitchenStatusKey(status);
        return k is "pendingApproval" or "pendingCashier";
    }

    /// <summary>Normalized kitchen status key (matches web KDS <c>kitchenStatusKey</c>).</summary>
    public static string KitchenStatusKey(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "other";

        var spaced = Regex.Replace(status.Trim(), "([a-z])([A-Z])", "$1 $2");
        var n = Regex.Replace(spaced, @"\s+", " ").Trim().ToLowerInvariant();
        return n switch
        {
            "waiting" => "waiting",
            "pending approval" => "pendingApproval",
            "pending cashier" => "pendingCashier",
            "in kitchen" => "inKitchen",
            "ready" => "ready",
            "served" => "served",
            _ => "other"
        };
    }

    /// <summary>Incoming kitchen column (KDS): <c>Waiting</c> only — after cashier release. Excludes pending cashier/approval.</summary>
    public static bool IsKitchenIncomingColumn(string? status) =>
        KitchenStatusKey(status) == "waiting";

    /// <summary>Orders visible on kitchen KDS (web + desktop + API when portal is KitchenBar).</summary>
    public static bool IsKitchenKdsVisibleStatus(string? status) =>
        IsKitchenQueueStatus(status);

    public static bool IsKitchenPreparingColumn(string? status) =>
        KitchenStatusKey(status) == "inKitchen";

    public static bool IsKitchenReadyColumn(string? status) =>
        KitchenStatusKey(status) == "ready";

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

    public static bool IsReady(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        string.Equals(status.Trim(), "Ready", StringComparison.OrdinalIgnoreCase);

    /// <summary>In-store: cashier records payment only after server marks <see cref="Served"/>. Online pickup/delivery: after kitchen marks <c>Ready</c> (no server handoff).</summary>
    public static bool CanCashierComplete(string? status) => IsServed(status);

    /// <inheritdoc cref="CanCashierComplete(string?)"/>
    public static bool CanCashierComplete(string? status, string? orderOrigin) =>
        OrderOrigin.IsOnline(orderOrigin)
            ? IsReady(status) || IsServed(status)
            : IsServed(status);

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
