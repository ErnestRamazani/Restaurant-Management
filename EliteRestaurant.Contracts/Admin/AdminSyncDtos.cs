using System.Text.Json;

namespace EliteRestaurant.Contracts.Admin;

public sealed record AdminSyncOperationDto(
    string IdempotencyKey,
    string EntityName,
    string Operation,
    JsonElement Payload,
    DateTime QueuedAtUtc);

public sealed record AdminSyncBatchRequest(IReadOnlyList<AdminSyncOperationDto> Operations);

public sealed record AdminSyncOperationResultDto(
    string IdempotencyKey,
    string EntityName,
    string Operation,
    bool Success,
    string? Message);

public sealed record AdminSyncBatchResponse(IReadOnlyList<AdminSyncOperationResultDto> Results);

public sealed record AdminEntitySnapshotDto(
    string EntityName,
    JsonElement Payload,
    DateTime SnapshotAtUtc);

public sealed record AdminEntityListResponse(
    string EntityName,
    IReadOnlyList<JsonElement> Items,
    DateTime SnapshotAtUtc);
