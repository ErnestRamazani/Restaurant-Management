using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Utils;

namespace EliteRestaurantPro.Services;

/// <summary>Persists create-order flows: phase validation, append to open check, new order — via cloud API only.</summary>
public sealed class OrderSubmissionService
{
    private readonly AdminOrdersApiClient _cloudOrders = new();
    private readonly AdminDataApiClient _data = new();

    public async Task<CreateOrderPhaseResult> LoadPhase1Async(CreateOrderSubmitSnapshot snap, CancellationToken cancellationToken = default)
    {
        if (string.Equals(snap.SelectedOrderSource, "Delivery", StringComparison.OrdinalIgnoreCase))
        {
            return new CreateOrderPhaseResult(
                true,
                "Create Order",
                string.Empty,
                0,
                "Delivery",
                new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));
        }

        var tablesTask = _data.GetTablesAsync(cancellationToken);
        var ordersTask = _data.GetOrdersAsync(cancellationToken);
        await Task.WhenAll(tablesTask, ordersTask).ConfigureAwait(false);

        var table = (await tablesTask.ConfigureAwait(false)).SingleOrDefault(t => t.Id == snap.TableId);
        if (table is null || table.AssignedServerId is null)
            return new CreateOrderPhaseResult(false, "Create Order", "Selected table must have an assigned server.", 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        var assigned = await _data.GetEmployeesAsync(cancellationToken).ConfigureAwait(false);
        var server = assigned.FirstOrDefault(e => e.Id == table.AssignedServerId.Value);
        if (server is null)
            return new CreateOrderPhaseResult(false, "Create Order", "Selected table must have an assigned server.", 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        if (AppSession.IsServerTablet && table.AssignedServerId != snap.ServerEmployeeId)
            return new CreateOrderPhaseResult(false, "Create Order", "This table is not assigned to your session.", 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        var allOrders = await ordersTask.ConfigureAwait(false);
        var open = allOrders
            .Where(o => o.TableId == table.Id && OrderWorkflow.IsOpenCheckStatus(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        var code = open is null ? string.Empty : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        var tableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;

        return new CreateOrderPhaseResult(
            true,
            "Create Order",
            string.Empty,
            table.TableNumber,
            tableName,
            new CreateOrderOpenCheckInfo(open?.Id, code, open?.Status ?? string.Empty));
    }

    public CreateOrderAppendResult AppendToExisting(CreateOrderSubmitSnapshot snap, int openOrderId)
    {
        try
        {
            var response = _cloudOrders.CreateOrderAsync(snap, appendToOpenCheck: true, openOrderId)
                .GetAwaiter()
                .GetResult();
            return new CreateOrderAppendResult(response.Success, response.Title, response.Message);
        }
        catch (Exception ex)
        {
            return new CreateOrderAppendResult(false, "Cloud API", ex.GetBaseException().Message);
        }
    }

    public CreateOrderSaveResult SaveNew(CreateOrderSubmitSnapshot snap)
    {
        try
        {
            var response = _cloudOrders.CreateOrderAsync(snap, appendToOpenCheck: false, openOrderId: null)
                .GetAwaiter()
                .GetResult();
            return new CreateOrderSaveResult(response.Success, response.Title, response.Message);
        }
        catch (Exception ex)
        {
            return new CreateOrderSaveResult(false, "Cloud API", ex.GetBaseException().Message);
        }
    }
}
