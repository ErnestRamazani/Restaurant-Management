using EliteRestaurant.Core.Models;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.ViewModels;

public sealed class ActivityFeedItem
{
    public ActivityItem Source { get; init; } = null!;
    public string KindLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DetailBlock { get; init; } = string.Empty;

    public static ActivityFeedItem From(ActivityItem source) => new()
    {
        Source = source,
        KindLabel = DashboardTextLocalizer.TranslateKind(source.KindLabel),
        Title = source.Title,
        DetailBlock = DashboardTextLocalizer.TranslateDetail(source.DetailBlock)
    };
}
