using EliteRestaurant.Core.Data;

namespace EliteRestaurant.Core.Utils;

/// <summary>
/// Legacy static entry points for desktop local DB. Prefer <see cref="SharedOrderDraftService"/> with injected <see cref="AppDbContext"/> on the API.
/// </summary>
public static class SharedOrderDraftStore
{
    public const string ServerPortal = SharedOrderDraftService.ServerPortal;

    public static int ParseTableIdFromSnapshotJson(string? json) =>
        SharedOrderDraftService.ParseTableIdFromSnapshotJson(json);

    public static SharedDraftRow? GetServerDraft(
        int employeeId,
        string draftUniqueId,
        bool restrictCustomerToAssignedServer = false) =>
        WithLocalDb(s => s.GetServerDraft(employeeId, draftUniqueId, restrictCustomerToAssignedServer));

    public static IReadOnlyList<SharedDraftRow> ListServerDrafts(
        int employeeId,
        int selectedTableId,
        bool restrictCustomerDraftToAssignedServer = false) =>
        WithLocalDb(s => s.ListServerDrafts(employeeId, selectedTableId, restrictCustomerDraftToAssignedServer));

    public static SharedDraftRow SaveServerDraft(int employeeId, string employeeName, string label, string payloadJson, int tableId = 0) =>
        WithLocalDb(s => s.SaveServerDraft(employeeId, employeeName, label, payloadJson, tableId));

    public static bool DeleteServerDraft(
        int employeeId,
        string draftUniqueId,
        int selectedTableId = 0,
        bool restrictCustomerDeleteToAssignedServer = false) =>
        WithLocalDb(s => s.DeleteServerDraft(employeeId, draftUniqueId, selectedTableId, restrictCustomerDeleteToAssignedServer));

    public static int PurgeDraftsOlderThan(TimeSpan maxAge) =>
        WithLocalDb(s => s.PurgeDraftsOlderThan(maxAge));

    public static int PurgeDraftsOlderThan(AppDbContext db, TimeSpan maxAge) =>
        new SharedOrderDraftService(db).PurgeDraftsOlderThan(maxAge);

    private static T WithLocalDb<T>(Func<SharedOrderDraftService, T> action)
    {
        using var db = new AppDbContext();
        return action(new SharedOrderDraftService(db));
    }
}
