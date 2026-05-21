using EliteRestaurant.Core.Models;
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
    public void CanCashierComplete_Online_AllowsReadyOrServed()
    {
        Assert.True(OrderWorkflow.CanCashierComplete("Ready", OrderOrigin.Online));
        Assert.True(OrderWorkflow.CanCashierComplete(OrderWorkflow.Served, OrderOrigin.Online));
        Assert.False(OrderWorkflow.CanCashierComplete("Waiting", OrderOrigin.Online));
        Assert.False(OrderWorkflow.CanCashierComplete("Ready", OrderOrigin.InStore));
    }

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

    [Theory]
    [InlineData("Waiting", true)]
    [InlineData("Pending approval", false)]
    [InlineData("PendingApproval", false)]
    [InlineData("Pending cashier", false)]
    [InlineData("In Kitchen", false)]
    [InlineData("Ready", false)]
    public void IsKitchenIncomingColumn_WaitingOnly(string status, bool expected) =>
        Assert.Equal(expected, OrderWorkflow.IsKitchenIncomingColumn(status));

    [Theory]
    [InlineData("In Kitchen", true)]
    [InlineData("InKitchen", true)]
    [InlineData("Waiting", false)]
    [InlineData("Ready", false)]
    public void IsKitchenPreparingColumn_Normalizes(string status, bool expected) =>
        Assert.Equal(expected, OrderWorkflow.IsKitchenPreparingColumn(status));

    [Theory]
    [InlineData("Waiting", true)]
    [InlineData("In Kitchen", true)]
    [InlineData("Ready", true)]
    [InlineData("Pending cashier", false)]
    [InlineData("Pending approval", false)]
    [InlineData("Served", false)]
    public void IsKitchenKdsVisibleStatus_ExcludesPreCashierRelease(string status, bool expected) =>
        Assert.Equal(expected, OrderWorkflow.IsKitchenKdsVisibleStatus(status));
}
