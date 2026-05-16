using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.ApiClients;

public sealed class AdminDataApiClient(EliteApiClient? apiClient = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EliteApiClient _apiClient = apiClient ?? new EliteApiClient();

    public void ReloadFromSettings() => _apiClient.ReloadFromSettings();

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

    /// <summary>Server filters by date (not the 1000-row snapshot cap). Matches reservation slot or last update in range.</summary>
    public Task<IReadOnlyList<ReservationBooking>> GetReservationsForReportRangeAsync(
        DateTime startInclusiveLocalDate,
        DateTime endInclusiveLocalDate,
        CancellationToken cancellationToken = default)
    {
        var s = startInclusiveLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var e = endInclusiveLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return GetListAsync<ReservationBooking>("reservations", cancellationToken, $"start={Uri.EscapeDataString(s)}&end={Uri.EscapeDataString(e)}");
    }

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

    /// <summary>Server filters by Money-aligned anchor (<c>PaymentConfirmedAt ?? CompletedAt ?? CreatedAt</c>), not the 1000 newest snapshot.</summary>
    public Task<IReadOnlyList<OrderRecord>> GetOrdersForReportRangeAsync(
        DateTime startInclusiveLocalDate,
        DateTime endInclusiveLocalDate,
        CancellationToken cancellationToken = default)
    {
        var s = startInclusiveLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var e = endInclusiveLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return GetListAsync<OrderRecord>("orders", cancellationToken, $"start={Uri.EscapeDataString(s)}&end={Uri.EscapeDataString(e)}");
    }

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

        var productsList = (await productsTask.ConfigureAwait(false)).ToList();
        await EnrichProductsWithMenuAvailabilityAsync(productsList, cancellationToken).ConfigureAwait(false);

        return (
            await tablesTask.ConfigureAwait(false),
            productsList,
            await reservationsTask.ConfigureAwait(false),
            await ordersTask.ConfigureAwait(false));
    }

    private async Task EnrichProductsWithMenuAvailabilityAsync(
        List<Product> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
            return;
        try
        {
            var map = await _apiClient.PostAsync<AdminProductIdsRequest, Dictionary<int, bool>>(
                    "api/admin/data/inventory/menu-product-availability",
                    new AdminProductIdsRequest(products.Select(p => p.Id).ToArray()),
                    cancellationToken)
                .ConfigureAwait(false);
            if (map is null)
                return;
            foreach (var p in products)
            {
                if (map.TryGetValue(p.Id, out var ok))
                    p.IsAvailable = ok;
            }
        }
        catch (HttpRequestException)
        {
            // Older API hosts without this route — leave IsAvailable at default true.
        }
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string entityName, CancellationToken cancellationToken, string? query = null)
    {
        var path = string.IsNullOrEmpty(query)
            ? $"api/admin/data/{entityName}"
            : $"api/admin/data/{entityName}?{query}";
        var response = await _apiClient.GetAsync<AdminEntityListResponse>(
            path,
            cancellationToken);

        return response?.Items
            .Select(item => item.Deserialize<T>(JsonOptions))
            .Where(item => item is not null)
            .Cast<T>()
            .ToList()
            ?? [];
    }
}
