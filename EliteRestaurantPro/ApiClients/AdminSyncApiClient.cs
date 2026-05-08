using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Sync;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminSyncApiClient(EliteApiClient? apiClient = null)
{
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public async Task<IReadOnlyList<CloudSyncResult>> PushAsync(
        IReadOnlyList<CloudSyncOperation> operations,
        CancellationToken cancellationToken = default)
    {
        var request = new AdminSyncBatchRequest(
            operations
                .Select(operation => new AdminSyncOperationDto(
                    operation.IdempotencyKey,
                    operation.EntityName,
                    operation.Operation,
                    JsonSerializer.Deserialize<JsonElement>(operation.PayloadJson),
                    operation.QueuedAtUtc))
                .ToList());

        var response = await _apiClient.PostAsync<AdminSyncBatchRequest, AdminSyncBatchResponse>(
            "api/admin/sync",
            request,
            cancellationToken);

        return response?.Results
            .Select(result => new CloudSyncResult(result.IdempotencyKey, result.Success, result.Message))
            .ToList()
            ?? [];
    }
}
