using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
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
                CreateOrderUiLocalizer.DialogTitle,
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
            return new CreateOrderPhaseResult(false, CreateOrderUiLocalizer.DialogTitle, CreateOrderUiLocalizer.ErrTableNeedsServer, 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        var assigned = await _data.GetEmployeesAsync(cancellationToken).ConfigureAwait(false);
        var server = assigned.FirstOrDefault(e => e.Id == table.AssignedServerId.Value);
        if (server is null)
            return new CreateOrderPhaseResult(false, CreateOrderUiLocalizer.DialogTitle, CreateOrderUiLocalizer.ErrTableNeedsServer, 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        if (AppSession.IsServerTablet && table.AssignedServerId != snap.ServerEmployeeId)
            return new CreateOrderPhaseResult(false, CreateOrderUiLocalizer.DialogTitle, CreateOrderUiLocalizer.ErrTableNotAssignedToYou, 0, string.Empty, new CreateOrderOpenCheckInfo(null, string.Empty, string.Empty));

        var allOrders = await ordersTask.ConfigureAwait(false);
        var open = allOrders
            .Where(o => o.TableId == table.Id && OrderWorkflow.IsOpenCheckStatus(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();
        var code = open is null ? string.Empty : string.IsNullOrWhiteSpace(open.UniqueId) ? $"#{open.Id:000}" : open.UniqueId;
        var tableName = string.IsNullOrWhiteSpace(table.Name) ? $"Table {table.TableNumber}" : table.Name;

        return new CreateOrderPhaseResult(
            true,
            CreateOrderUiLocalizer.DialogTitle,
            string.Empty,
            table.TableNumber,
            tableName,
            new CreateOrderOpenCheckInfo(open?.Id, code, open?.Status ?? string.Empty));
    }

    public async Task<CreateOrderAppendResult> AppendToExistingAsync(
        CreateOrderSubmitSnapshot snap,
        int openOrderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cloudOrders
                .CreateOrderAsync(snap, appendToOpenCheck: true, openOrderId, cancellationToken)
                .ConfigureAwait(false);
            return MapCreateOrderResponseToAppendResult(response);
        }
        catch (Exception ex)
        {
            return new CreateOrderAppendResult(false, CreateOrderUiLocalizer.CloudApiCaption, ex.GetBaseException().Message);
        }
    }

    public async Task<CreateOrderSaveResult> SaveNewAsync(
        CreateOrderSubmitSnapshot snap,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cloudOrders
                .CreateOrderAsync(snap, appendToOpenCheck: false, openOrderId: null, cancellationToken)
                .ConfigureAwait(false);
            return MapCreateOrderResponseToSaveResult(response);
        }
        catch (Exception ex)
        {
            return new CreateOrderSaveResult(false, CreateOrderUiLocalizer.CloudApiCaption, ex.GetBaseException().Message);
        }
    }

    private static CreateOrderAppendResult MapCreateOrderResponseToAppendResult(AdminCreateOrderResponse response)
    {
        if (response.Success)
            return new CreateOrderAppendResult(true, response.Title, response.Message);

        var (caption, message) = FriendlyCreateOrderFailure(response);
        return new CreateOrderAppendResult(false, caption, message);
    }

    private static CreateOrderSaveResult MapCreateOrderResponseToSaveResult(AdminCreateOrderResponse response)
    {
        if (response.Success)
            return new CreateOrderSaveResult(true, response.Title, response.Message);

        var (caption, message) = FriendlyCreateOrderFailure(response);
        return new CreateOrderSaveResult(false, caption, message);
    }

    /// <summary>Turns API titles/messages into short dialog captions and readable body text (no raw JSON).</summary>
    private static (string Caption, string Message) FriendlyCreateOrderFailure(AdminCreateOrderResponse response)
    {
        if (string.Equals(response.Title, "Insufficient Inventory", StringComparison.OrdinalIgnoreCase))
        {
            var body = (response.Message ?? string.Empty).Trim();
            return (CreateOrderUiLocalizer.InsufficientInventoryTitle, CreateOrderUiLocalizer.InsufficientInventoryBody(body));
        }

        return (response.Title, response.Message);
    }
}
