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

    [Theory]
    [InlineData("Pending approval", true)]
    [InlineData("PENDING APPROVAL", true)]
    [InlineData("Pending cashier", false)]
    public void IsPendingApproval_Normalizes(string? status, bool expected) =>
        Assert.Equal(expected, OrderWorkflow.IsPendingApproval(status));

    [Fact]
    public void AwaitsCashierOrApproval_IncludesBothPendings()
    {
        Assert.True(OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen(OrderWorkflow.PendingCashier));
        Assert.True(OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen(OrderWorkflow.PendingApproval));
        Assert.False(OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen("Waiting"));
    }

    [Fact]
    public void OccupiesTable_IncludesPendingApproval() =>
        Assert.True(OrderWorkflow.OccupiesTable(OrderWorkflow.PendingApproval));
}
