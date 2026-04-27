using System.Text.Json;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Services;

public sealed class CreateOrderDraftItemPayload
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>JSON payload for server create-order drafts (shared with SharedOrderDraftStore).</summary>
public sealed class CreateOrderDraftPayload
{
    public string DraftLabel { get; set; } = string.Empty;
    public int SelectedTableId { get; set; }
    public string SelectedOrderSource { get; set; } = "WalkIn";
    public string SelectedDeliveryReference { get; set; } = string.Empty;
    public string SelectedReservationCode { get; set; } = string.Empty;
    public string SelectedOrderStatus { get; set; } = "Waiting";
    public string SelectedOrderCategory { get; set; } = "All";
    public string SelectedOrderSubCategory { get; set; } = "All";
    public string ProductSearchText { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;
    public string AllergyNotes { get; set; } = string.Empty;
    public string SelectedPaymentCurrency { get; set; } = CurrencyHelper.Usd;
    public string DiscountMode { get; set; } = "None";
    public string DiscountInput { get; set; } = string.Empty;
    public List<CreateOrderDraftItemPayload> Items { get; set; } = [];
}

public sealed class DraftPersistenceService
{
    public static string Serialize(CreateOrderDraftPayload payload) =>
        JsonSerializer.Serialize(payload);

    public static CreateOrderDraftPayload? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CreateOrderDraftPayload>(json);
        }
        catch
        {
            return null;
        }
    }

    public SharedDraftRow Save(int employeeId, string employeeName, CreateOrderDraftPayload payload) =>
        SharedOrderDraftStore.SaveServerDraft(employeeId, employeeName, payload.DraftLabel, Serialize(payload), payload.SelectedTableId);

    public static IReadOnlyList<SharedDraftRow> ListForEmployee(int employeeId, int selectedTableId, bool restrictCustomerDraftToAssignedServer) =>
        SharedOrderDraftStore.ListServerDrafts(employeeId, selectedTableId, restrictCustomerDraftToAssignedServer);

    public static bool TryGetPayload(int employeeId, string draftUniqueId, int selectedTableId, bool restrictCustomerDraftToAssignedServer, out SharedDraftRow? row)
    {
        row = SharedOrderDraftStore.ListServerDrafts(employeeId, selectedTableId, restrictCustomerDraftToAssignedServer)
            .FirstOrDefault(d => string.Equals(d.Id, draftUniqueId, StringComparison.Ordinal));
        return row is not null;
    }

    public static bool Delete(int employeeId, string draftUniqueId, int selectedTableId, bool restrictCustomerDeleteToAssignedServer) =>
        SharedOrderDraftStore.DeleteServerDraft(employeeId, draftUniqueId, selectedTableId, restrictCustomerDeleteToAssignedServer);
}
