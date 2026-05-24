using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.ApiClients;

/// <summary>Cloud API for create-order drafts when the desktop has no local PostgreSQL.</summary>
public sealed class AdminOrderDraftApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _api = apiClient ?? new EliteApiClient();

    public IReadOnlyList<SharedDraftRow> List(
        int employeeId,
        int selectedTableId,
        bool restrictCustomerDraftToAssignedServer)
    {
        var path =
            $"api/admin/data/order-drafts?employeeId={employeeId}&tableId={selectedTableId}&restrictCustomer={(restrictCustomerDraftToAssignedServer ? "true" : "false")}";
        var rows = _api.GetAsync<List<AdminOrderDraftDto>>(path).GetAwaiter().GetResult() ?? [];
        return rows
            .Select(d => new SharedDraftRow(d.Id, d.Label, d.SnapshotJson, d.UpdatedAtUtc, d.TableId, d.IsCustomerDraft))
            .ToList();
    }

    public SharedDraftRow Save(int employeeId, string employeeName, string label, string payloadJson, int tableId)
    {
        var body = new AdminSaveOrderDraftRequest(employeeId, employeeName, label, payloadJson);
        var saved = _api
            .PostAsync<AdminSaveOrderDraftRequest, AdminOrderDraftDto>("api/admin/data/order-drafts", body)
            .GetAwaiter()
            .GetResult();
        if (saved is null)
            throw new InvalidOperationException("Cloud API returned an empty draft save response.");

        return new SharedDraftRow(
            saved.Id,
            saved.Label,
            saved.SnapshotJson,
            saved.UpdatedAtUtc,
            saved.TableId,
            saved.IsCustomerDraft);
    }

    public bool Delete(int employeeId, string draftUniqueId, int selectedTableId, bool restrictCustomerDeleteToAssignedServer)
    {
        var path =
            $"api/admin/data/order-drafts/{Uri.EscapeDataString(draftUniqueId)}?employeeId={employeeId}&tableId={selectedTableId}&restrictCustomer={(restrictCustomerDeleteToAssignedServer ? "true" : "false")}";
        return _api.DeleteAsync(path).GetAwaiter().GetResult();
    }
}
