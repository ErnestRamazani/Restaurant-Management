using EliteRestaurant.Core.Models;

namespace EliteRestaurantPro.ApiClients;

public sealed class ProductsApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetProductsAsync(cancellationToken);
}

public sealed class EmployeesApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetEmployeesAsync(cancellationToken);
}

public sealed class TablesApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<Table>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetTablesAsync(cancellationToken);
}

public sealed class ReservationsApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<ReservationBooking>> GetReservationsAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetReservationsAsync(cancellationToken);
}

public sealed class InventoryApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<InventoryItem>> GetInventoryItemsAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetInventoryItemsAsync(cancellationToken);
}

public sealed class AttendanceApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<EmployeeAttendance>> GetAttendanceAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetAttendanceAsync(cancellationToken);
}

public sealed class PayrollApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<SalaryAdvance>> GetSalaryAdvancesAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetSalaryAdvancesAsync(cancellationToken);
    public Task<IReadOnlyList<PayrollPaymentRecord>> GetPayrollAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetPayrollAsync(cancellationToken);
}

public sealed class OrdersAdminApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<OrderRecord>> GetOrdersAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetOrdersAsync(cancellationToken);
}

public sealed class ReportsApiClient(AdminDataApiClient? dataClient = null)
{
    private readonly AdminDataApiClient _dataClient = dataClient ?? new AdminDataApiClient();
    public Task<IReadOnlyList<MoneyTransaction>> GetMoneyTransactionsAsync(CancellationToken cancellationToken = default) =>
        _dataClient.GetMoneyTransactionsAsync(cancellationToken);
}
