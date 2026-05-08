namespace EliteRestaurant.Core.Sync;

public sealed record CloudSyncOperation(
    string IdempotencyKey,
    string EntityName,
    string Operation,
    string PayloadJson,
    DateTime QueuedAtUtc);

public sealed record CloudSyncResult(
    string IdempotencyKey,
    bool Success,
    string? Message);
