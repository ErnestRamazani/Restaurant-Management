using System.Net.Http.Json;
using System.Text.Json;

namespace EliteRestaurantPro.ApiClients;

public sealed class CashierReservationsApiClient(EliteApiClient? apiClient = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EliteApiClient _api = apiClient ?? new EliteApiClient();

    public async Task<IReadOnlyList<CashierEngagementListRow>> ListEngagementsAsync(CancellationToken cancellationToken = default)
        => await _api.GetAsync<IReadOnlyList<CashierEngagementListRow>>("api/cashier/reservations/engagements", cancellationToken)
            .ConfigureAwait(false)
           ?? [];

    public Task<CashierEngagementDetailDto?> GetEngagementAsync(int id, CancellationToken cancellationToken = default)
        => _api.GetAsync<CashierEngagementDetailDto>($"api/cashier/reservations/engagements/{id}", cancellationToken);

    public Task MarkArrivedAsync(int id, CancellationToken cancellationToken = default)
        => PostNoBodyAsync($"api/cashier/reservations/engagements/{id}/arrived", cancellationToken);

    public Task MarkNoShowAsync(int id, CancellationToken cancellationToken = default)
        => PostNoBodyAsync($"api/cashier/reservations/engagements/{id}/no-show", cancellationToken);

    public Task MarkCancelledAsync(int id, CancellationToken cancellationToken = default)
        => PostNoBodyAsync($"api/cashier/reservations/engagements/{id}/cancel", cancellationToken);

    public Task RescheduleAsync(int id, DateTime plannedStartUtc, CancellationToken cancellationToken = default)
        => PostJsonAsync(
            $"api/cashier/reservations/engagements/{id}/reschedule",
            new CashierRescheduleEngagementRequest(plannedStartUtc, null),
            cancellationToken);

    private async Task PostNoBodyAsync(string path, CancellationToken cancellationToken)
    {
        await PostJsonAsync(path, new { }, cancellationToken).ConfigureAwait(false);
    }

    private async Task PostJsonAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken)
    {
        await _api.PostAsync<TRequest, JsonElement>(path, body, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record CashierEngagementListRow(
    int Id,
    string? ConfirmationCode,
    string Status,
    string GuestName,
    string GuestPhone,
    int PartySize,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    string TableLabel,
    int PlacementUnitId);

public sealed record CashierEngagementDetailDto(
    int Id,
    string? ConfirmationCode,
    string Status,
    string GuestName,
    string GuestPhone,
    string GuestEmail,
    int PartySize,
    string UserNotes,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    DateTime? ActualStartUtc,
    DateTime? ActualEndUtc,
    int TableId,
    string TableLabel,
    int PlacementUnitId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CashierRescheduleEngagementRequest(
    DateTime PlannedStartUtc,
    DateTime? PlannedEndUtc);
