using EliteRestaurant.Core.Utils;
using Xunit;

namespace EliteRestaurant.Tests;

public class OrderWorkflowTests
{
    [Theory]
    [InlineData("Pending cashier", true)]
    [InlineData("PENDING CASHIER", true)]
    [InlineData("Waiting", false)]
    [InlineData("", false)]
    public void IsPendingCashier_Normalizes(string? status, bool expected) =>
        Assert.Equal(expected, OrderWorkflow.IsPendingCashier(status));

    [Fact]
    public void CanCashierComplete_OnlyWhenServed() =>
        Assert.True(OrderWorkflow.CanCashierComplete(OrderWorkflow.Served));

    [Fact]
    public void OccupiesTable_IncludesKitchenPipeline() =>
        Assert.True(OrderWorkflow.OccupiesTable("Waiting"));

    [Fact]
    public void CanAdminAdvanceOrderStatus_IncludesReady() =>
        Assert.True(OrderWorkflow.CanAdminAdvanceOrderStatus("Ready"));
}
