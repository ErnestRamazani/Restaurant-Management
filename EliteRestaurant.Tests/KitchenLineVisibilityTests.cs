using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class KitchenLineVisibilityTests
{
    private static OrderItem Line(int id, DateTime? preparedAt = null) => new()
    {
        Id = id,
        ProductId = id,
        Quantity = 1,
        KitchenPreparedAt = preparedAt
    };

    [Fact]
    public void Summarize_FirstCycle_NoCardSummary()
    {
        var items = new[] { Line(1), Line(2) };
        var work = KitchenLineVisibility.Summarize(items);
        Assert.False(work.HighlightNewOnTicket);
        Assert.Equal(2, work.NewCount);
        Assert.Equal(0, work.PreparedCount);
        Assert.Empty(work.CardSummaryText);
    }

    [Fact]
    public void Summarize_AfterAppend_ShowsNewAndPreparedCounts()
    {
        var prepared = DateTime.UtcNow.AddHours(-1);
        var items = new[]
        {
            Line(1, prepared),
            Line(2, prepared),
            Line(3),
            Line(4)
        };
        var work = KitchenLineVisibility.Summarize(items);
        Assert.True(work.HighlightNewOnTicket);
        Assert.Equal(2, work.NewCount);
        Assert.Equal(2, work.PreparedCount);
        Assert.Equal("2 new items · 2 already prepared", work.CardSummaryText);
    }

    [Fact]
    public void IsNewForKitchen_OnlyUnpreparedLinesWhenTicketHasPriorWork()
    {
        var prepared = DateTime.UtcNow;
        var items = new[] { Line(1, prepared), Line(2), Line(3, prepared) };
        Assert.False(KitchenLineVisibility.IsNewForKitchen(items[0], items));
        Assert.True(KitchenLineVisibility.IsNewForKitchen(items[1], items));
        Assert.False(KitchenLineVisibility.IsNewForKitchen(items[2], items));
    }

    [Fact]
    public void MarkUnpreparedLinesPrepared_StampsOnlyNullLines()
    {
        var stamp = new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<OrderItem>
        {
            Line(1, stamp),
            Line(2),
            Line(3)
        };
        KitchenLineVisibility.MarkUnpreparedLinesPrepared(items, stamp.AddMinutes(5));
        Assert.Equal(stamp, items[0].KitchenPreparedAt);
        Assert.Equal(stamp.AddMinutes(5), items[1].KitchenPreparedAt);
        Assert.Equal(stamp.AddMinutes(5), items[2].KitchenPreparedAt);
    }

    [Fact]
    public void KitchenOrderQueueMapper_ProjectsLineFlags()
    {
        var prepared = DateTime.UtcNow.AddHours(-2);
        var order = new OrderRecord
        {
            Id = 9,
            UniqueId = "ORD-9",
            Status = "Waiting",
            Items =
            [
                Line(1, prepared),
                Line(2)
            ]
        };
        var row = KitchenOrderQueueMapper.ToQueueRow(order);
        Assert.Equal("1 new item · 1 already prepared", row.KitchenWorkSummary);
        Assert.True(row.Items.Single(i => i.Id == 2).IsNewForKitchen);
        Assert.False(row.Items.Single(i => i.Id == 1).IsNewForKitchen);
        Assert.Equal(KitchenLineVisibility.LineStatusPrepared, row.Items.Single(i => i.Id == 1).KitchenLineStatus);
    }
}
