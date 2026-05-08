using System.Net.Http;
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

    public Task<IReadOnlyList<ProductIngredient>> GetProductIngredientsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ProductIngredient>("productingredients", cancellationToken);

    public Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Employee>("employees", cancellationToken);

    public Task<IReadOnlyList<Table>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<Table>("tables", cancellationToken);

    public Task<IReadOnlyList<ReservationBooking>> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<ReservationBooking>("reservations", cancellationToken);

    public Task<IReadOnlyList<CustomerProfile>> GetCustomerProfilesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<CustomerProfile>("customerprofiles", cancellationToken);

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

    /// <summary>
    /// Older API deployments may not expose this entity yet. Tries alternate routes, then returns an empty list on 404 so Attendance still loads.
    /// </summary>
    public async Task<IReadOnlyList<AttendanceDayValidation>> GetAttendanceDayValidationsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entity in new[] { "attendancedayvalidations", "attendancevalidations", "attendancedayvalidation" })
        {
            try
            {
                return await GetListAsync<AttendanceDayValidation>(entity, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("(404)", StringComparison.Ordinal))
            {
            }
        }

        return [];
    }

    public Task<IReadOnlyList<MoneyTransaction>> GetMoneyTransactionsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<MoneyTransaction>("money", cancellationToken);

    /// <summary>
    /// One HTTP round-trip when the API supports <c>bundles/create-order</c>; otherwise four parallel list calls (older hosts).
    /// </summary>
    public async Task<(IReadOnlyList<Table> Tables, IReadOnlyList<Product> Products, IReadOnlyList<ReservationBooking> Reservations, IReadOnlyList<OrderRecord> Orders)> GetCreateOrderCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var bundle = await _apiClient.TryGetCreateOrderCatalogBundleAsync(cancellationToken).ConfigureAwait(false);
        if (bundle is not null)
        {
            List<T> Map<T>(IReadOnlyList<System.Text.Json.JsonElement> items) where T : class =>
                items
                    .Select(item => item.Deserialize<T>(JsonOptions))
                    .Where(x => x is not null)
                    .Cast<T>()
                    .ToList();

            return (
                Map<Table>(bundle.Tables),
                Map<Product>(bundle.Products),
                Map<ReservationBooking>(bundle.Reservations),
                Map<OrderRecord>(bundle.Orders));
        }

        var tablesTask = GetTablesAsync(cancellationToken);
        var productsTask = GetProductsAsync(cancellationToken);
        var reservationsTask = GetReservationsAsync(cancellationToken);
        var ordersTask = GetOrdersAsync(cancellationToken);
        await Task.WhenAll(tablesTask, productsTask, reservationsTask, ordersTask).ConfigureAwait(false);

        return (
            await tablesTask.ConfigureAwait(false),
            await productsTask.ConfigureAwait(false),
            await reservationsTask.ConfigureAwait(false),
            await ordersTask.ConfigureAwait(false));
    }

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
