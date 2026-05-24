namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminOrderDraftDto(
    string Id,
    string Label,
    string SnapshotJson,
    DateTime UpdatedAtUtc,
    int TableId,
    bool IsCustomerDraft);

public sealed record AdminSaveOrderDraftRequest(
    int EmployeeId,
    string EmployeeName,
    string Label,
    string SnapshotJson);
