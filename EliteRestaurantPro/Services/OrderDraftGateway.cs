using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.Services;

/// <summary>
/// Order drafts on local PostgreSQL when configured; otherwise the cloud admin API
/// (required after cloud-only / no local DB).
/// </summary>
public static class OrderDraftGateway
{
    private static readonly AdminOrderDraftApiClient CloudDrafts = new();

    public static IReadOnlyList<SharedDraftRow> List(
        int employeeId,
        int selectedTableId,
        bool restrictCustomerDraftToAssignedServer) =>
        AppDbContext.LocalDatabaseConfigured
            ? SharedOrderDraftStore.ListServerDrafts(employeeId, selectedTableId, restrictCustomerDraftToAssignedServer)
            : CloudDrafts.List(employeeId, selectedTableId, restrictCustomerDraftToAssignedServer);

    public static SharedDraftRow Save(int employeeId, string employeeName, string label, string payloadJson, int tableId) =>
        AppDbContext.LocalDatabaseConfigured
            ? SharedOrderDraftStore.SaveServerDraft(employeeId, employeeName, label, payloadJson, tableId)
            : CloudDrafts.Save(employeeId, employeeName, label, payloadJson, tableId);

    public static bool Delete(
        int employeeId,
        string draftUniqueId,
        int selectedTableId,
        bool restrictCustomerDeleteToAssignedServer) =>
        AppDbContext.LocalDatabaseConfigured
            ? SharedOrderDraftStore.DeleteServerDraft(employeeId, draftUniqueId, selectedTableId, restrictCustomerDeleteToAssignedServer)
            : CloudDrafts.Delete(employeeId, draftUniqueId, selectedTableId, restrictCustomerDeleteToAssignedServer);
}
