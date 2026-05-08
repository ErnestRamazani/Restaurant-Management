using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminDataApiClient(EliteApiClient? apiClient = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Product>("products", cancellationToken);

    public Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Employee>("employees", cancellationToken);

    public Task<IReadOnlyList<Table>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Table>("tables", cancellationToken);

    public Task<IReadOnlyList<ReservationBooking>> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ReservationBooking>("reservations", cancellationToken);

    public Task<IReadOnlyList<InventoryItem>> GetInventoryItemsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<InventoryItem>("inventory", cancellationToken);

    public Task<IReadOnlyList<EmployeeAttendance>> GetAttendanceAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<EmployeeAttendance>("attendance", cancellationToken);

    public Task<IReadOnlyList<SalaryAdvance>> GetSalaryAdvancesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<SalaryAdvance>("salaryadvances", cancellationToken);

    public Task<IReadOnlyList<PayrollPaymentRecord>> GetPayrollAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<PayrollPaymentRecord>("payroll", cancellationToken);

    public Task<IReadOnlyList<OrderRecord>> GetOrdersAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<OrderRecord>("orders", cancellationToken);

    public Task<IReadOnlyList<MoneyTransaction>> GetMoneyTransactionsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<MoneyTransaction>("money", cancellationToken);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string entityName, CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync<AdminEntityListResponse>(
            $"api/admin/data/{entityName}",
            cancellationToken);

        return response?.Items
            .Select(item => item.Deserialize<T>(JsonOptions))
            .Where(item => item is not null)
            .Cast<T>()
            .ToList()
            ?? [];
    }
}
