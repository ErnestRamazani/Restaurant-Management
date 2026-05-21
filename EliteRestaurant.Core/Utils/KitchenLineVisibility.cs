using EliteRestaurant.Core.Models;

namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Distinguishes line items that still need kitchen work from lines already prepared on a prior kitchen cycle
/// (e.g. after server append on an open check).
/// </summary>
public static class KitchenLineVisibility
{
    public const string LineStatusNew = "new";
    public const string LineStatusPrepared = "prepared";

    public static bool IsLinePrepared(OrderItem item) =>
        item.KitchenPreparedAt is not null;

    public static bool OrderHasPriorKitchenWork(IEnumerable<OrderItem> items) =>
        items.Any(IsLinePrepared);

    /// <summary>
    /// True when this line still needs prep and the ticket already has prepared lines (re-fire after append).
    /// </summary>
    public static bool IsNewForKitchen(OrderItem item, IEnumerable<OrderItem> allItems) =>
        !IsLinePrepared(item) && OrderHasPriorKitchenWork(allItems);

    public static string KitchenLineStatus(OrderItem item) =>
        IsLinePrepared(item) ? LineStatusPrepared : LineStatusNew;

    public static KitchenWorkSummary Summarize(IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
            return new KitchenWorkSummary(0, 0, false, string.Empty);

        var prepared = items.Count(IsLinePrepared);
        var newCount = items.Count(i => !IsLinePrepared(i));
        var hasPrior = prepared > 0;
        var highlightNew = hasPrior && newCount > 0;
        var summary = highlightNew
            ? $"{newCount} new item{(newCount == 1 ? "" : "s")} · {prepared} already prepared"
            : string.Empty;
        return new KitchenWorkSummary(newCount, prepared, highlightNew, summary);
    }

    public static void MarkUnpreparedLinesPrepared(IEnumerable<OrderItem> items, DateTime? preparedAt = null)
    {
        var stamp = preparedAt ?? DateTime.UtcNow;
        foreach (var item in items)
        {
            if (item.KitchenPreparedAt is null)
                item.KitchenPreparedAt = stamp;
        }
    }
}

public readonly record struct KitchenWorkSummary(
    int NewCount,
    int PreparedCount,
    bool HighlightNewOnTicket,
    string CardSummaryText);
