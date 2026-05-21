using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using EliteRestaurant.Core.Utils;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Reporting;
using EliteRestaurantPro.ApiClients;
using Microsoft.Win32;

namespace EliteRestaurantPro.ViewModels;

public sealed class ReportEntityItem
{
    public int Id { get; init; }
    public string UniqueId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
}

public sealed class ReportTimeEntryDto
{
    public DateTime EventTime { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string RelatedInfo { get; init; } = string.Empty;
    public string EntityContext { get; init; } = string.Empty;
    public int OrdersCount { get; init; }
    public int ItemCount { get; init; }
    public decimal UnitUsage { get; init; }
    public string EventTimeText => EventTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    public string MetricsText => UnitUsage > 0m
        ? $"{UnitUsage:0.##} units"
        : ItemCount > 0
            ? $"{ItemCount} items"
            : OrdersCount > 0
                ? $"{OrdersCount} order(s)"
                : "-";
}

public sealed class ReportDayGroupDto
{
    public DateTime Day { get; init; }
    public string DayText { get; init; } = string.Empty;
    public string TotalsText { get; init; } = string.Empty;
    public ObservableCollection<ReportTimeEntryDto> Entries { get; init; } = new();
}

public sealed class ReportsViewModel : AdminBaseViewModel
{
    private static readonly DateTime OrdersHistoryCatalogStart = DateTime.Today.AddYears(-5);

    private readonly AdminDataApiClient _data = new();
    private ReportEntityItem? _selectedEmployee;
    private ReportEntityItem? _selectedTable;
    private ReportEntityItem? _selectedInventoryItem;
    private ReportEntityItem? _selectedMenuItem;

    private string _employeeSummary = "Select an employee to view details.";
    private string _tableSummary = "Select a table to view details.";
    private string _inventorySummary = "Select an inventory item to view details.";
    private string _menuSummary = "Select a menu item to view details.";
    private string _dailySummary = "Daily report is ready.";
    private string _ordersSummary = "Orders report is ready.";

    private string _employeeNotes = "-";
    private string _employeePayrollHistory = "-";
    private string _tableCurrentServer = "-";
    private string _inventoryNotes = "-";
    private string _menuIngredientsSummary = "-";
    private DateTime _reportStartDate = DateTime.Today.AddDays(-14);
    private DateTime _reportEndDate = DateTime.Today;
    private string _selectedExportReportType = "Daily";

    public override string ActivePage => "Reports";

    public ObservableCollection<ReportEntityItem> Employees { get; } = new();
    public ObservableCollection<ReportEntityItem> Tables { get; } = new();
    public ObservableCollection<ReportEntityItem> InventoryItems { get; } = new();
    public ObservableCollection<ReportEntityItem> MenuItems { get; } = new();

    public ObservableCollection<ReportDayGroupDto> EmployeeTimelineDays { get; } = new();
    public ObservableCollection<ReportDayGroupDto> TableTimelineDays { get; } = new();
    public ObservableCollection<ReportDayGroupDto> InventoryTimelineDays { get; } = new();
    public ObservableCollection<ReportDayGroupDto> MenuTimelineDays { get; } = new();
    public ObservableCollection<ReportDayGroupDto> DailyTimelineDays { get; } = new();
    public ObservableCollection<ReportDayGroupDto> OrderTimelineDays { get; } = new();
    public ObservableCollection<string> ExportReportTypes { get; } = new(["Daily", "Orders", "Employees", "Tables", "Inventory", "Menu", "All Reports"]);

    public ReportEntityItem? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (!SetField(ref _selectedEmployee, value))
                return;
            _ = LoadEmployeeDetailsAsync(value?.Id);
        }
    }

    public ReportEntityItem? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (!SetField(ref _selectedTable, value))
                return;
            _ = LoadTableDetailsAsync(value?.Id);
        }
    }

    public ReportEntityItem? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            if (!SetField(ref _selectedInventoryItem, value))
                return;
            _ = LoadInventoryDetailsAsync(value?.Id);
        }
    }

    public ReportEntityItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (!SetField(ref _selectedMenuItem, value))
                return;
            _ = LoadMenuDetailsAsync(value?.Id);
        }
    }

    public string DailySummary
    {
        get => _dailySummary;
        set => SetField(ref _dailySummary, value);
    }

    public string OrdersSummary
    {
        get => _ordersSummary;
        set => SetField(ref _ordersSummary, value);
    }

    public string EmployeeSummary
    {
        get => _employeeSummary;
        set => SetField(ref _employeeSummary, value);
    }

    public string EmployeeNotes
    {
        get => _employeeNotes;
        set => SetField(ref _employeeNotes, value);
    }

    public string EmployeePayrollHistory
    {
        get => _employeePayrollHistory;
        set => SetField(ref _employeePayrollHistory, value);
    }

    public string TableSummary
    {
        get => _tableSummary;
        set => SetField(ref _tableSummary, value);
    }

    public string TableCurrentServer
    {
        get => _tableCurrentServer;
        set => SetField(ref _tableCurrentServer, value);
    }

    public string InventorySummary
    {
        get => _inventorySummary;
        set => SetField(ref _inventorySummary, value);
    }

    public string InventoryNotes
    {
        get => _inventoryNotes;
        set => SetField(ref _inventoryNotes, value);
    }

    public string MenuSummary
    {
        get => _menuSummary;
        set => SetField(ref _menuSummary, value);
    }

    public string MenuIngredientsSummary
    {
        get => _menuIngredientsSummary;
        set => SetField(ref _menuIngredientsSummary, value);
    }

    public DateTime ReportStartDate
    {
        get => _reportStartDate;
        set
        {
            if (!SetField(ref _reportStartDate, value))
                return;
            _ = LoadDailyReportAsync();
            _ = LoadOrdersReportAsync();
        }
    }

    public DateTime ReportEndDate
    {
        get => _reportEndDate;
        set
        {
            if (!SetField(ref _reportEndDate, value))
                return;
            _ = LoadDailyReportAsync();
            _ = LoadOrdersReportAsync();
        }
    }

    public string SelectedExportReportType
    {
        get => _selectedExportReportType;
        set => SetField(ref _selectedExportReportType, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand BulkExportExcelCommand { get; }

    public ReportsViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        RefreshCommand = new RelayCommand(_ => _ = LoadReportListsAsync());
        ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync());
        BulkExportExcelCommand = new RelayCommand(_ => _ = ExportBulkExcelAsync());
        _ = LoadReportListsAsync();
    }

    private async Task<Dictionary<int, Product>> LoadProductsByIdAsync() =>
        (await _data.GetProductsAsync().ConfigureAwait(true)).ToDictionary(p => p.Id);

    private static void HydrateOrderItems(IReadOnlyList<OrderRecord> orders, IReadOnlyDictionary<int, Product> productsById)
    {
        foreach (var o in orders)
        {
            foreach (var i in o.Items)
            {
                i.OrderRecord = o;
                if (productsById.TryGetValue(i.ProductId, out var p))
                    i.Product = p;
            }
        }
    }

    private async Task<List<OrderRecord>> LoadOrdersInRangeAsync(DateTime start, DateTime endExclusive)
    {
        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var endInclusive = endExclusive.AddDays(-1).Date;
        var orders = (await _data.GetOrdersForReportRangeAsync(start.Date, endInclusive).ConfigureAwait(true))
            .Where(o => OrderReportAnchor.IsAnchorInHalfOpenLocalRange(o, start, endExclusive))
            .OrderBy(OrderReportAnchor.Anchor)
            .ToList();
        HydrateOrderItems(orders, productsById);
        return orders;
    }

    private async Task LoadReportListsAsync()
    {
        var employeesTask = _data.GetEmployeesAsync();
        var tablesTask = _data.GetTablesAsync();
        var invTask = _data.GetInventoryItemsAsync();
        var productsTask = _data.GetProductsAsync();
        await Task.WhenAll(employeesTask, tablesTask, invTask, productsTask).ConfigureAwait(true);

        var employees = (await employeesTask.ConfigureAwait(true)).OrderBy(e => e.Name).ToList();
        Employees.Clear();
        foreach (var employee in employees)
        {
            Employees.Add(new ReportEntityItem
            {
                Id = employee.Id,
                UniqueId = employee.UniqueId,
                Name = employee.Name,
                Subtitle = employee.Role
            });
        }

        var tables = (await tablesTask.ConfigureAwait(true)).OrderBy(t => t.TableNumber).ToList();
        Tables.Clear();
        foreach (var table in tables)
        {
            Tables.Add(new ReportEntityItem
            {
                Id = table.Id,
                UniqueId = table.UniqueId,
                Name = $"Table {table.TableNumber} - {table.Name}",
                Subtitle = table.Status
            });
        }

        var inv = (await invTask.ConfigureAwait(true)).OrderBy(i => i.Name).ToList();
        InventoryItems.Clear();
        foreach (var item in inv)
        {
            InventoryItems.Add(new ReportEntityItem
            {
                Id = item.Id,
                UniqueId = item.UniqueId,
                Name = item.Name,
                Subtitle = $"{item.StockQuantity:0.##} {item.Unit}"
            });
        }

        var products = (await productsTask.ConfigureAwait(true)).OrderBy(p => p.Name).ToList();
        MenuItems.Clear();
        foreach (var product in products)
        {
            MenuItems.Add(new ReportEntityItem
            {
                Id = product.Id,
                UniqueId = product.UniqueId,
                Name = product.Name,
                Subtitle = $"{product.Category} | $ {product.Price:0.00}"
            });
        }

        SelectedEmployee = Employees.FirstOrDefault();
        SelectedTable = Tables.FirstOrDefault();
        SelectedInventoryItem = InventoryItems.FirstOrDefault();
        SelectedMenuItem = MenuItems.FirstOrDefault();
        await LoadDailyReportAsync().ConfigureAwait(true);
        await LoadOrdersReportAsync().ConfigureAwait(true);
    }

    private async Task LoadOrdersReportAsync()
    {
        OrderTimelineDays.Clear();
        var start = ReportStartDate.Date;
        var endExclusive = ReportEndDate.Date.AddDays(1);
        if (endExclusive <= start)
        {
            OrdersSummary = "Set a valid date range (end on or after start).";
            return;
        }

        var orders = await LoadOrdersInRangeAsync(start, endExclusive).ConfigureAwait(true);
        var entries = new List<ReportTimeEntryDto>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join(", ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, "None", 0m);
            var grandUsd = totals.GrandTotal;
            var payUsd = order.PaymentAmountUsd > 0m
                ? order.PaymentAmountUsd
                : (order.PaymentAmountFc <= 0m ? grandUsd : 0m);
            var payFc = order.PaymentAmountFc;
            var paymentText = CurrencyHelper.FormatDualCurrency(payUsd, payFc);
            var anchor = OrderReportAnchor.Anchor(order);

            entries.Add(new ReportTimeEntryDto
            {
                EventTime = anchor,
                EventType = string.IsNullOrWhiteSpace(order.Status) ? "Order" : order.Status,
                Summary = $"Order {DisplayOrFallback(order.UniqueId, $"#{order.Id}")} · {totalQty} item(s)",
                RelatedInfo =
                    $"{OrderRecordUiLabels.ServerCaption(order)} | {OrderRecordUiLabels.TableCaption(order)} | {DisplayOrFallback(menu, "No line items")}",
                EntityContext = paymentText,
                OrdersCount = 1,
                ItemCount = totalQty
            });
        }

        ApplyOrderDayGroups(OrderTimelineDays, entries);

        var dayCount = entries.Count == 0 ? 0 : entries.GroupBy(e => e.EventTime.Date).Count();
        OrdersSummary =
            $"Range {start:yyyy-MM-dd} → {ReportEndDate:yyyy-MM-dd}. {orders.Count} order(s) over {dayCount} day(s), {entries.Sum(e => e.ItemCount)} items total.";
    }

    private async Task LoadEmployeeDetailsAsync(int? employeeId)
    {
        EmployeeTimelineDays.Clear();
        EmployeeSummary = "Select an employee to view details.";
        EmployeeNotes = "-";
        EmployeePayrollHistory = "-";

        if (employeeId is null)
            return;

        var employee = (await _data.GetEmployeesAsync().ConfigureAwait(true)).SingleOrDefault(e => e.Id == employeeId.Value);
        if (employee is null)
            return;

        EmployeeNotes = string.IsNullOrWhiteSpace(employee.Notes) ? "-" : employee.Notes;

        var payrollLines = (await _data.GetPayrollAsync().ConfigureAwait(true))
            .Where(p => p.EmployeeId == employee.Id)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Take(18)
            .ToList();
        EmployeePayrollHistory = payrollLines.Count == 0
            ? "No saved payroll yet. Confirm monthly payment on the Salary screen to record pay here."
            : string.Join(
                "\n",
                payrollLines.Select(p =>
                {
                    var paidLine = p.PaidToDateUsd >= p.NetPayUsd - 0.005m
                        ? $"Paid in full ${p.NetPayUsd:N2} USD"
                        : $"Partial: ${p.PaidToDateUsd:N2} of ${p.NetPayUsd:N2} USD net";
                    return
                        $"{PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month)}: {paidLine} (base gross ${p.MonthlySalaryUsd:N2}, sales ${p.MoneyGeneratedUsd:N2}, sales bonus ${p.BonusFivePercentUsd:N2}, advances -${p.AdvancesDeductedUsd:N2}) — last posting {p.PaidAtUtc.ToLocalTime():yyyy-MM-dd}";
                }));
        var entries = new List<ReportTimeEntryDto>();

        var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true))
            .Where(a => a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.WorkDate)
            .Take(120)
            .ToList();

        foreach (var row in attendanceRows)
        {
            foreach (var ev in AttendanceReportEntries.BuildForEmployeeDetail(row, employee.Name))
            {
                entries.Add(new ReportTimeEntryDto
                {
                    EventTime = ev.EventTime,
                    EventType = ev.EventType,
                    Summary = ev.Summary,
                    RelatedInfo = ev.RelatedInfo,
                    EntityContext = ev.EntityContext
                });
            }
        }

        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var servedOrders = (await _data.GetOrdersForReportRangeAsync(OrdersHistoryCatalogStart, DateTime.Today).ConfigureAwait(true))
            .Where(o => o.ServerId == employee.Id)
            .OrderByDescending(OrderReportAnchor.Anchor)
            .Take(180)
            .ToList();
        HydrateOrderItems(servedOrders, productsById);

        foreach (var order in servedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = anchor,
                EventType = "Served Order",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"{OrderRecordUiLabels.TableCaption(order)} | {DisplayOrFallback(items, "No order items.")}",
                EntityContext = employee.Name,
                OrdersCount = 1,
                ItemCount = totalItems
            });
        }

        var empMarker = $"| EMP:{employee.Id}|";
        var salaryMoneyRows = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true))
            .Where(t =>
                MoneyTransactionReportHelper.IsSalaryExpense(t) &&
                t.Justification?.Contains(empMarker, StringComparison.Ordinal) == true)
            .OrderByDescending(t => MoneyTransactionReportHelper.ToLocalInstant(t.Date))
            .Take(48)
            .ToList();

        foreach (var t in salaryMoneyRows)
        {
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = MoneyTransactionReportHelper.ToLocalInstant(t.Date),
                EventType = MoneyTransactionReportHelper.LedgerEventType(t),
                Summary = t.Justification ?? string.Empty,
                RelatedInfo = $"USD $ {t.AmountUsd:0.00}",
                EntityContext = employee.Name
            });
        }

        var advanceRows = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true))
            .Where(a => a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.GivenAt)
            .Take(36)
            .ToList();

        foreach (var adv in advanceRows)
        {
            var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                : "(by date)";
            var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                : "Pending deduction on payroll confirm";
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = adv.GivenAt,
                EventType = "Salary advance (record)",
                Summary = $"${adv.AmountUsd:0.00} for payroll {period} — {applied}",
                RelatedInfo = DisplayOrFallback(adv.Note, "—"),
                EntityContext = employee.Name
            });
        }

        ApplyDayGroups(EmployeeTimelineDays, entries);

        EmployeeSummary =
            $"Name: {employee.Name}\nID: {employee.UniqueId}\nRole: {employee.Role}\nStatus: {employee.EmploymentStatus}\nPhone: {employee.PhoneNumber}\nClock-in / sign-in events: {attendanceRows.Count}\nOrders Served: {servedOrders.Count}\nItems Served: {servedOrders.Sum(o => o.Items.Sum(i => i.Quantity))}";
    }

    private async Task LoadTableDetailsAsync(int? tableId)
    {
        TableTimelineDays.Clear();
        TableSummary = "Select a table to view details.";
        TableCurrentServer = "-";

        if (tableId is null)
            return;

        var table = (await _data.GetTablesAsync().ConfigureAwait(true)).SingleOrDefault(t => t.Id == tableId.Value);
        if (table is null)
            return;

        var employeesById = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToDictionary(e => e.Id);
        Employee? assignedServer = table.AssignedServer;
        if (assignedServer is null && table.AssignedServerId is int sid && employeesById.TryGetValue(sid, out var linked))
            assignedServer = linked;

        var entries = new List<ReportTimeEntryDto>();
        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var relatedOrders = (await _data.GetOrdersForReportRangeAsync(OrdersHistoryCatalogStart, DateTime.Today).ConfigureAwait(true))
            .Where(o => o.TableId == table.Id)
            .OrderByDescending(OrderReportAnchor.Anchor)
            .Take(220)
            .ToList();
        HydrateOrderItems(relatedOrders, productsById);

        foreach (var order in relatedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = anchor,
                EventType = "Table Order",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"Server: {OrderRecordUiLabels.ServerCaption(order)} | {DisplayOrFallback(items, "No order items.")}",
                EntityContext = $"Table {table.TableNumber}",
                OrdersCount = 1,
                ItemCount = totalItems
            });
        }

        var relatedReservations = (await _data.GetReservationsAsync().ConfigureAwait(true))
            .Where(r => r.TableId == table.Id)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(180)
            .ToList();

        foreach (var reservation in relatedReservations)
        {
            var name = string.IsNullOrWhiteSpace(reservation.ReservationName)
                ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                : reservation.ReservationName.Trim();
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = reservation.UpdatedAt,
                EventType = "Reservation",
                Summary = $"{name} ({reservation.Status})",
                RelatedInfo = $"Reservation {reservation.UniqueId} | Guest: {DisplayOrFallback(reservation.GuestName, "-")} | Party: {reservation.PartySize}",
                EntityContext = $"Table {table.TableNumber}"
            });
        }

        ApplyDayGroups(TableTimelineDays, entries);

        TableSummary =
            $"Table: {table.TableNumber} ({table.Name})\nID: {table.UniqueId}\nCapacity: {table.Capacity}\nStatus: {table.Status}\nOrders Logged: {relatedOrders.Count}\nItems Served: {relatedOrders.Sum(o => o.Items.Sum(i => i.Quantity))}";
        TableCurrentServer = assignedServer is null
            ? "Unassigned"
            : $"{assignedServer.Name} ({assignedServer.UniqueId})";
    }

    private async Task LoadInventoryDetailsAsync(int? inventoryId)
    {
        InventoryTimelineDays.Clear();
        InventorySummary = "Select an inventory item to view details.";
        InventoryNotes = "-";

        if (inventoryId is null)
            return;

        var item = (await _data.GetInventoryItemsAsync().ConfigureAwait(true)).SingleOrDefault(i => i.Id == inventoryId.Value);
        if (item is null)
            return;

        InventoryNotes = string.IsNullOrWhiteSpace(item.Notes) ? "-" : item.Notes;

        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var ingredients = (await _data.GetProductIngredientsAsync().ConfigureAwait(true))
            .Where(pi => pi.InventoryItemId == item.Id)
            .ToList();
        foreach (var pi in ingredients)
        {
            if (productsById.TryGetValue(pi.ProductId, out var p))
                pi.Product = p;
        }

        var entries = new List<ReportTimeEntryDto>();

        AppendInventoryNotesTimeline(item.Notes, entries, item.Name);
        ApplyDayGroups(InventoryTimelineDays, entries);

        InventorySummary =
            $"Item: {item.Name}\nID: {item.UniqueId}\nQuantity: {item.StockQuantity:0.##} {item.Unit}\nExpiration: {item.ExpirationDate?.ToString("yyyy-MM-dd") ?? "Not set"}\nLinked Menu Items: {ingredients.Select(i => i.ProductId).Distinct().Count()}";
    }

    private async Task LoadMenuDetailsAsync(int? productId)
    {
        MenuTimelineDays.Clear();
        MenuSummary = "Select a menu item to view details.";
        MenuIngredientsSummary = "-";

        if (productId is null)
            return;

        var product = (await _data.GetProductsAsync().ConfigureAwait(true)).SingleOrDefault(p => p.Id == productId.Value);
        if (product is null)
            return;

        var invById = (await _data.GetInventoryItemsAsync().ConfigureAwait(true)).ToDictionary(i => i.Id);
        var ingredients = (await _data.GetProductIngredientsAsync().ConfigureAwait(true))
            .Where(pi => pi.ProductId == product.Id)
            .ToList();
        foreach (var pi in ingredients)
        {
            if (invById.TryGetValue(pi.InventoryItemId, out var inv))
                pi.InventoryItem = inv;
        }

        MenuIngredientsSummary = ingredients.Count == 0
            ? "No ingredients linked."
            : string.Join(", ", ingredients.Select(i => $"{i.InventoryItem?.Name ?? "Unknown"} ({i.Quantity:0.##} {i.InventoryItem?.Unit ?? "unit"})"));

        var entries = new List<ReportTimeEntryDto>();
        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var allOrders = (await _data.GetOrdersForReportRangeAsync(OrdersHistoryCatalogStart, DateTime.Today).ConfigureAwait(true))
            .OrderByDescending(o => OrderReportAnchor.Anchor(o))
            .ToList();
        HydrateOrderItems(allOrders, productsById);

        var servedLines = allOrders
            .SelectMany(o => o.Items.Where(i => i.ProductId == product.Id).Select(i => (Order: o, Item: i)))
            .OrderByDescending(x => OrderReportAnchor.Anchor(x.Order))
            .Take(300)
            .ToList();

        foreach (var line in servedLines)
        {
            var order = line.Order;
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = anchor,
                EventType = "Menu Ordered",
                Summary = $"{product.Name} x{line.Item.Quantity} in order {order.UniqueId}",
                RelatedInfo = $"{OrderRecordUiLabels.TableCaption(order)} | Server: {OrderRecordUiLabels.ServerCaption(order)} | Status: {order.Status}",
                EntityContext = product.Name,
                OrdersCount = 1,
                ItemCount = line.Item.Quantity
            });
        }

        ApplyDayGroups(MenuTimelineDays, entries);

        var totalQty = servedLines.Sum(l => l.Item.Quantity);
        MenuSummary =
            $"Menu Item: {product.Name}\nID: {product.UniqueId}\nCategory: {product.Category}\nSub Category: {product.SubCategory}\nPrice: $ {product.Price:0.00}\nTimes Ordered: {servedLines.Count}\nUnits Ordered: {totalQty}";
    }

    private async Task LoadDailyReportAsync()
    {
        DailyTimelineDays.Clear();
        var start = ReportStartDate.Date;
        var endExclusive = ReportEndDate.Date.AddDays(1);
        if (endExclusive <= start)
        {
            DailySummary = "Set a valid date range (end on or after start) for the Daily tab.";
            return;
        }

        var rangeStartUtc = AttendanceCalendar.DayAnchorUtc(start);
        var rangeEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive);

        var entries = new List<ReportTimeEntryDto>();
        var employeesById = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToDictionary(e => e.Id);

        var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true))
            .Where(a => a.WorkDate >= rangeStartUtc && a.WorkDate < rangeEndExclusiveUtc)
            .OrderByDescending(a => a.WorkDate)
            .ToList();

        foreach (var row in attendanceRows)
        {
            if (row.Employee is null && employeesById.TryGetValue(row.EmployeeId, out var emp))
                row.Employee = emp;

            var name = row.Employee?.Name ?? "Unknown";
            foreach (var ev in AttendanceReportEntries.Build(row, name))
            {
                entries.Add(new ReportTimeEntryDto
                {
                    EventTime = ev.EventTime,
                    EventType = ev.EventType,
                    Summary = ev.Summary,
                    RelatedInfo = ev.RelatedInfo,
                    EntityContext = ev.EntityContext
                });
            }
        }

        var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
        var endInclusive = endExclusive.AddDays(-1).Date;
        var orders = (await _data.GetOrdersForReportRangeAsync(start, endInclusive).ConfigureAwait(true))
            .Where(o => OrderReportAnchor.IsAnchorInHalfOpenLocalRange(o, start, endExclusive))
            .OrderByDescending(o => OrderReportAnchor.Anchor(o))
            .ToList();
        HydrateOrderItems(orders, productsById);

        foreach (var order in orders)
        {
            var itemQty = order.Items.Sum(i => i.Quantity);
            var anchor = OrderReportAnchor.Anchor(order);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = anchor,
                EventType = OrderOrigin.IsOnline(order.OrderOrigin) ? "Online order" : "Table Service",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"{OrderRecordUiLabels.TableCaption(order)} | Server: {OrderRecordUiLabels.ServerCaption(order)} | Items: {itemQty}",
                EntityContext = $"Table: {OrderRecordUiLabels.TableCaption(order)}",
                OrdersCount = 1,
                ItemCount = itemQty
            });
        }

        var tablesById = (await _data.GetTablesAsync().ConfigureAwait(true)).ToDictionary(t => t.Id);
        var reservations = (await _data.GetReservationsForReportRangeAsync(start, endInclusive).ConfigureAwait(true))
            .OrderByDescending(r => r.ReservedFor)
            .ToList();

        foreach (var reservation in reservations)
        {
            if (reservation.Table is null && reservation.TableId is int tid && tablesById.TryGetValue(tid, out var tbl))
                reservation.Table = tbl;

            var displayName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                : reservation.ReservationName.Trim();
            var tableLabel = reservation.Table is not null
                ? $"{reservation.Table.TableNumber} ({DisplayOrFallback(reservation.Table.Name, "-")})"
                : "-";
            var resEvent = ReservationReportTime.DisplayEventTime(reservation, start, endExclusive);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = resEvent,
                EventType = "Reservation",
                Summary = $"Reservation {reservation.UniqueId} · {displayName} · {reservation.Status}",
                RelatedInfo = $"Reserved for {reservation.ReservedFor:yyyy-MM-dd HH:mm} | Party: {reservation.PartySize} | Table: {tableLabel}",
                EntityContext = "Reservations"
            });
        }

        foreach (var order in orders)
        {
            var anchor = OrderReportAnchor.Anchor(order);
            foreach (var line in order.Items)
            {
                entries.Add(new ReportTimeEntryDto
                {
                    EventTime = anchor,
                    EventType = "Menu Activity",
                    Summary = $"{line.Product?.Name ?? "Unknown"} x{line.Quantity}",
                    RelatedInfo = $"Order {order.UniqueId} | {OrderRecordUiLabels.TableCaption(order)}",
                    EntityContext = $"Menu: {line.Product?.Name ?? "Unknown"}",
                    OrdersCount = 1,
                    ItemCount = line.Quantity
                });
            }
        }

        var inventoryNoteRows = (await _data.GetInventoryItemsAsync().ConfigureAwait(true))
            .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
            .Select(i => new InventoryNotesSnapshot(i.UniqueId, i.Name, i.Notes!))
            .ToList();
        AppendInventoryActivityFromNotes(entries, start, endExclusive, inventoryNoteRows);

        var payrollRows = (await _data.GetPayrollAsync().ConfigureAwait(true))
            .Where(p =>
            {
                var paidLocal = p.PaidAtUtc.ToLocalTime();
                return paidLocal >= start && paidLocal < endExclusive;
            })
            .OrderByDescending(p => p.PaidAtUtc)
            .ToList();

        foreach (var p in payrollRows)
        {
            if (p.Employee is null && employeesById.TryGetValue(p.EmployeeId, out var empPay))
                p.Employee = empPay;

            var paidLocal = p.PaidAtUtc.ToLocalTime();
            var name = p.Employee?.Name ?? $"Employee #{p.EmployeeId}";
            var monthLabel = PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month);
            var paidLine = p.PaidToDateUsd >= p.NetPayUsd - 0.005m
                ? $"Paid in full ${p.NetPayUsd:N2} USD net"
                : $"Paid to date ${p.PaidToDateUsd:N2} of ${p.NetPayUsd:N2} USD net";
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = paidLocal,
                EventType = "Payroll month (record)",
                Summary = $"{name} · {monthLabel} · {paidLine}",
                RelatedInfo =
                    $"Base gross ${p.MonthlySalaryUsd:N2} · Sales ${p.MoneyGeneratedUsd:N2} · Bonus ${p.BonusFivePercentUsd:N2} · Advances deducted ${p.AdvancesDeductedUsd:N2}",
                EntityContext = "Payroll"
            });
        }

        var advanceRows = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true))
            .Where(a =>
            {
                var given = MoneyTransactionReportHelper.ToLocalInstant(a.GivenAt);
                return given >= start && given < endExclusive;
            })
            .OrderByDescending(a => a.GivenAt)
            .ToList();

        foreach (var adv in advanceRows)
        {
            if (adv.Employee is null && employeesById.TryGetValue(adv.EmployeeId, out var empAdv))
                adv.Employee = empAdv;

            var givenLocal = MoneyTransactionReportHelper.ToLocalInstant(adv.GivenAt);
            var advName = adv.Employee?.Name ?? $"Employee #{adv.EmployeeId}";
            var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                : "(by date)";
            var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                : "Pending deduction on payroll confirm";
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = givenLocal,
                EventType = "Salary advance (record)",
                Summary = $"{advName} · ${adv.AmountUsd:0.00} USD for payroll {period} — {applied}",
                RelatedInfo = DisplayOrFallback(adv.Note, "—"),
                EntityContext = "Salary advances"
            });
        }

        var salaryTx = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true))
            .Where(t =>
            {
                if (!MoneyTransactionReportHelper.IsSalaryExpense(t))
                    return false;
                var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
                return local >= start && local < endExclusive;
            })
            .OrderByDescending(t => MoneyTransactionReportHelper.ToLocalInstant(t.Date))
            .ThenByDescending(t => t.Id)
            .ToList();

        foreach (var t in salaryTx)
        {
            _ = MoneyTransactionReportHelper.TryParseEmployeeIdFromSalaryJustification(t.Justification ?? string.Empty, out var eid);
            employeesById.TryGetValue(eid, out var empMoney);
            var who = empMoney?.Name ?? string.Empty;
            var label = MoneyTransactionReportHelper.LedgerEventType(t);
            var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = local,
                EventType = label,
                Summary = string.IsNullOrWhiteSpace(who)
                    ? (t.Justification ?? string.Empty)
                    : $"{who} · {t.Justification}",
                RelatedInfo = $"USD $ {t.AmountUsd:0.00} (Money ledger)",
                EntityContext = "Money"
            });
        }

        ApplyDayGroups(DailyTimelineDays, entries, dailyPayrollPinnedDaySort: true);
        DailySummary =
            $"Daily timeline {start:yyyy-MM-dd} → {ReportEndDate:yyyy-MM-dd}: {entries.Count} events (clock-ins/sign-ins, orders, reservations, menu, inventory, payroll records, salary advances, Money salary ledger).";
    }

    private static void ApplyDayGroups(
        ObservableCollection<ReportDayGroupDto> target,
        IEnumerable<ReportTimeEntryDto> entries,
        bool dailyPayrollPinnedDaySort = false)
    {
        target.Clear();
        if (dailyPayrollPinnedDaySort)
        {
            foreach (var dayGroup in entries
                .GroupBy(e => OrderReportAnchor.LocalCalendarDay(e.EventTime))
                .OrderByDescending(g => g.Key))
            {
                var rows = dayGroup.ToList();
                rows.Sort((a, b) => ReportDailyTimelineSort.CompareWithinDayPayrollFirstNewestFirst(
                    a.EventTime,
                    a.EventType,
                    b.EventTime,
                    b.EventType));
                target.Add(new ReportDayGroupDto
                {
                    Day = dayGroup.Key,
                    DayText = dayGroup.Key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                    TotalsText =
                        $"{rows.Count} events | {rows.Sum(r => r.OrdersCount)} orders | {rows.Sum(r => r.ItemCount)} items | {rows.Sum(r => r.UnitUsage):0.##} units",
                    Entries = new ObservableCollection<ReportTimeEntryDto>(rows)
                });
            }

            return;
        }

        var normalized = entries
            .OrderByDescending(e => e.EventTime)
            .ThenBy(e => e.EventType)
            .ToList();

        foreach (var dayGroup in normalized
            .GroupBy(e => OrderReportAnchor.LocalCalendarDay(e.EventTime))
            .OrderByDescending(g => g.Key))
        {
            var rows = dayGroup.ToList();
            target.Add(new ReportDayGroupDto
            {
                Day = dayGroup.Key,
                DayText = dayGroup.Key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                TotalsText =
                    $"{rows.Count} events | {rows.Sum(r => r.OrdersCount)} orders | {rows.Sum(r => r.ItemCount)} items | {rows.Sum(r => r.UnitUsage):0.##} units",
                Entries = new ObservableCollection<ReportTimeEntryDto>(rows)
            });
        }
    }

    /// <summary>Most recent calendar day first; within each day, oldest order first.</summary>
    private static void ApplyOrderDayGroups(ObservableCollection<ReportDayGroupDto> target, List<ReportTimeEntryDto> entries)
    {
        target.Clear();
        foreach (var dayGroup in entries.GroupBy(e => e.EventTime.Date).OrderByDescending(g => g.Key))
        {
            var rows = dayGroup.OrderBy(e => e.EventTime).ThenBy(e => e.Summary).ToList();
            var orderCount = rows.Sum(r => r.OrdersCount);
            target.Add(new ReportDayGroupDto
            {
                Day = dayGroup.Key,
                DayText = dayGroup.Key.ToString("dddd, MMM dd yyyy", CultureInfo.InvariantCulture),
                TotalsText = $"{orderCount} order(s) | {rows.Sum(r => r.ItemCount)} items",
                Entries = new ObservableCollection<ReportTimeEntryDto>(rows)
            });
        }
    }

    /// <summary>
    /// Adds timestamped lines from <see cref="InventoryItem.Notes"/> (manual adjustments, order deductions, etc.)
    /// to the daily feed. Only lines with a leading <c>yyyy-MM-dd HH:mm</c> timestamp in range are included.
    /// </summary>
    private static void AppendInventoryActivityFromNotes(
        List<ReportTimeEntryDto> entries,
        DateTime start,
        DateTime endExclusive,
        IReadOnlyList<InventoryNotesSnapshot> inventoryNoteRows)
    {
        foreach (var row in inventoryNoteRows)
        {
            if (string.IsNullOrWhiteSpace(row.Notes))
                continue;

            foreach (var rawLine in row.Notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var ts = TryParseLeadingTimestamp(line);
                if (ts is null || ts.Value < start || ts.Value >= endExclusive)
                    continue;

                entries.Add(new ReportTimeEntryDto
                {
                    EventTime = ts.Value,
                    EventType = "Inventory Activity",
                    Summary = line,
                    RelatedInfo = $"Item: {row.Name} ({row.UniqueId})",
                    EntityContext = $"Inventory: {row.Name}"
                });
            }
        }
    }

    private sealed record InventoryNotesSnapshot(string UniqueId, string Name, string Notes);

    private static void AppendInventoryNotesTimeline(string notes, ICollection<ReportTimeEntryDto> entries, string itemName)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return;

        var lines = notes
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            var timestamp = TryParseLeadingTimestamp(line);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = timestamp ?? DateTime.Today.AddHours(12),
                EventType = "Inventory Note",
                Summary = line,
                RelatedInfo = "Text-based inventory history entry",
                EntityContext = itemName
            });
        }
    }

    private static DateTime? TryParseLeadingTimestamp(string line)
    {
        if (line.Length < 16)
            return null;

        var maybeDate = line[..16];
        if (DateTime.TryParseExact(
                maybeDate,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string DisplayOrFallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private async Task ExportExcelAsync()
    {
        var range = TryGetExportRange();
        if (range is null)
            return;

        if (SelectedExportReportType == "All Reports")
        {
            await ExportBulkExcelAsync().ConfigureAwait(true);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Excel Report",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"{SelectedExportReportType.ToLowerInvariant()}-report-{ReportStartDate:yyyyMMdd}-{ReportEndDate:yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var data = await BuildExportRowsAsync(SelectedExportReportType, range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        ExcelExportService.ExportSingleSheet(saveDialog.FileName, SelectedExportReportType, data.Headers, data.Rows);
    }

    private async Task ExportBulkExcelAsync()
    {
        var range = TryGetExportRange();
        if (range is null)
            return;

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Bulk Excel Report",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"reports-bulk-{ReportStartDate:yyyyMMdd}-{ReportEndDate:yyyyMMdd}.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var daily = await BuildExportRowsAsync("Daily", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        var orders = await BuildExportRowsAsync("Orders", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        var employees = await BuildExportRowsAsync("Employees", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        var tables = await BuildExportRowsAsync("Tables", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        var inventory = await BuildExportRowsAsync("Inventory", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);
        var menu = await BuildExportRowsAsync("Menu", range.Value.Start, range.Value.EndExclusive).ConfigureAwait(true);

        var sheets = new List<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)>
        {
            ("Daily", daily.Headers, daily.Rows),
            ("Orders", orders.Headers, orders.Rows),
            ("Employees", employees.Headers, employees.Rows),
            ("Tables", tables.Headers, tables.Rows),
            ("Inventory", inventory.Headers, inventory.Rows),
            ("Menu", menu.Headers, menu.Rows)
        };

        ExcelExportService.ExportWorkbook(saveDialog.FileName, sheets);
    }

    private (DateTime Start, DateTime EndExclusive)? TryGetExportRange()
    {
        var start = ReportStartDate.Date;
        var end = ReportEndDate.Date;
        if (end < start)
        {
            System.Windows.MessageBox.Show("End date must be after start date.", "Report Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return null;
        }

        return (start, end.AddDays(1));
    }

    private async Task<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)> BuildExportRowsAsync(string reportType, DateTime start, DateTime endExclusive)
    {
        if (reportType == "Orders")
            return BuildOrderExportRows(await LoadOrdersInRangeAsync(start, endExclusive).ConfigureAwait(true));

        var employeesById = (await _data.GetEmployeesAsync().ConfigureAwait(true)).ToDictionary(e => e.Id, e => e);
        var tablesById = (await _data.GetTablesAsync().ConfigureAwait(true)).ToDictionary(t => t.Id, t => t);

        var rows = new List<IReadOnlyList<string>>();

        bool includeAttendance = reportType is "Daily" or "Employees";
        bool includeOrders = reportType is "Daily" or "Employees" or "Tables" or "Menu";
        bool includeReservations = reportType is "Daily" or "Tables";

        if (includeAttendance)
        {
            var attendanceStartUtc = AttendanceCalendar.DayAnchorUtc(start.Date);
            var attendanceEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive.Date);
            var attendanceRows = (await _data.GetAttendanceAsync().ConfigureAwait(true))
                .Where(a => a.WorkDate >= attendanceStartUtc && a.WorkDate < attendanceEndExclusiveUtc)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => a.EmployeeId)
                .ToList();

            foreach (var row in attendanceRows)
            {
                if (row.Employee is null && employeesById.TryGetValue(row.EmployeeId, out var empAttach))
                    row.Employee = empAttach;

                var empName = row.Employee?.Name ?? string.Empty;
                foreach (var ev in AttendanceReportEntries.Build(row, empName))
                {
                    rows.Add(BuildAnalyticalRow(
                        eventTime: ev.EventTime,
                        eventType: ev.EventType,
                        employeeId: row.Employee?.UniqueId ?? string.Empty,
                        employeeName: empName,
                        orderId: ev.Summary.Length > 120 ? ev.Summary[..120] + "…" : ev.Summary,
                        costOrPrice: string.Empty));
                }
            }
        }

        var includeSalaryLedger = reportType is "Daily" or "Employees";
        if (includeSalaryLedger)
        {
            var salaryTx = (await _data.GetMoneyTransactionsAsync().ConfigureAwait(true))
                .Where(t =>
                {
                    if (!MoneyTransactionReportHelper.IsSalaryExpense(t))
                        return false;
                    var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
                    return local >= start && local < endExclusive;
                })
                .OrderBy(t => MoneyTransactionReportHelper.ToLocalInstant(t.Date))
                .ThenBy(t => t.Id)
                .ToList();

            foreach (var t in salaryTx)
            {
                _ = MoneyTransactionReportHelper.TryParseEmployeeIdFromSalaryJustification(t.Justification ?? string.Empty, out var eid);
                employeesById.TryGetValue(eid, out var empRef);
                var j = t.Justification ?? string.Empty;
                if (j.Length > 120)
                    j = j[..120] + "…";

                var local = MoneyTransactionReportHelper.ToLocalInstant(t.Date);
                rows.Add(BuildAnalyticalRow(
                    eventTime: local,
                    eventType: MoneyTransactionReportHelper.LedgerEventType(t),
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? string.Empty,
                    orderId: j,
                    costOrPrice: t.AmountUsd.ToString("0.00", CultureInfo.InvariantCulture)));
            }

            var payrollRows = (await _data.GetPayrollAsync().ConfigureAwait(true))
                .Where(p =>
                {
                    var paidLocal = p.PaidAtUtc.ToLocalTime();
                    return paidLocal >= start && paidLocal < endExclusive;
                })
                .OrderBy(p => p.PaidAtUtc)
                .ToList();

            foreach (var p in payrollRows)
            {
                employeesById.TryGetValue(p.EmployeeId, out var empRef);
                var paidLocal = p.PaidAtUtc.ToLocalTime();
                var monthLabel = PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month);
                var detail =
                    $"{monthLabel} · paid ${p.PaidToDateUsd:0.00} / net ${p.NetPayUsd:0.00} · base ${p.MonthlySalaryUsd:0.00} · sales ${p.MoneyGeneratedUsd:0.00} · bonus ${p.BonusFivePercentUsd:0.00} · adv -${p.AdvancesDeductedUsd:0.00}";
                rows.Add(BuildAnalyticalRow(
                    eventTime: paidLocal,
                    eventType: "Payroll month (record)",
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? string.Empty,
                    orderId: detail));
            }

            var advanceRows = (await _data.GetSalaryAdvancesAsync().ConfigureAwait(true))
                .Where(a =>
                {
                    var given = MoneyTransactionReportHelper.ToLocalInstant(a.GivenAt);
                    return given >= start && given < endExclusive;
                })
                .OrderBy(a => MoneyTransactionReportHelper.ToLocalInstant(a.GivenAt))
                .ThenBy(a => a.Id)
                .ToList();

            foreach (var adv in advanceRows)
            {
                employeesById.TryGetValue(adv.EmployeeId, out var empRef);
                var givenLocal = MoneyTransactionReportHelper.ToLocalInstant(adv.GivenAt);
                var period = adv.ForPayrollYear.HasValue && adv.ForPayrollMonth.HasValue
                    ? PayrollCalculator.FormatPayrollMonthLabel(adv.ForPayrollYear.Value, adv.ForPayrollMonth.Value)
                    : "(by date)";
                var applied = adv.AppliedPayrollYear.HasValue && adv.AppliedPayrollMonth.HasValue
                    ? $"Applied to {PayrollCalculator.FormatPayrollMonthLabel(adv.AppliedPayrollYear.Value, adv.AppliedPayrollMonth.Value)}"
                    : "Pending deduction on payroll confirm";
                var note = string.IsNullOrWhiteSpace(adv.Note) ? "—" : adv.Note.Trim();
                rows.Add(BuildAnalyticalRow(
                    eventTime: givenLocal,
                    eventType: "Salary advance (record)",
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? string.Empty,
                    orderId: $"${adv.AmountUsd:0.00} for {period} — {applied} — {note}"));
            }
        }

        List<OrderItem> orderItems = new();
        if (includeOrders)
        {
            var productsById = await LoadProductsByIdAsync().ConfigureAwait(true);
            var endInclusive = endExclusive.AddDays(-1).Date;
            var ordersForLines = (await _data.GetOrdersForReportRangeAsync(start.Date, endInclusive).ConfigureAwait(true))
                .Where(o => OrderReportAnchor.IsAnchorInHalfOpenLocalRange(o, start, endExclusive))
                .OrderBy(o => OrderReportAnchor.Anchor(o))
                .ToList();
            HydrateOrderItems(ordersForLines, productsById);
            orderItems = ordersForLines
                .SelectMany(o => o.Items)
                .OrderBy(i => i.OrderRecord is null ? DateTime.MinValue : OrderReportAnchor.Anchor(i.OrderRecord))
                .ThenBy(i => i.Id)
                .ToList();
        }

        if (includeOrders)
        {
            foreach (var line in orderItems)
            {
                var order = line.OrderRecord;
                if (order is null)
                    continue;

                employeesById.TryGetValue(line.PreparedByEmployeeId ?? 0, out var preparedByEmployee);
                employeesById.TryGetValue(order.ServerId ?? 0, out var serverEmployee);
                tablesById.TryGetValue(order.TableId ?? 0, out var table);

                var anchor = OrderReportAnchor.Anchor(order);
                rows.Add(BuildAnalyticalRow(
                    eventTime: anchor,
                    eventType: "Order",
                    employeeId: preparedByEmployee?.UniqueId ?? string.Empty,
                    employeeName: line.PreparedByName,
                    serverId: serverEmployee?.UniqueId ?? string.Empty,
                    serverName: OrderRecordUiLabels.ServerCaption(order),
                    orderId: order.UniqueId,
                    tableId: table?.UniqueId ?? string.Empty,
                    tableName: OrderRecordUiLabels.TableCaption(order),
                    productId: line.Product?.UniqueId ?? string.Empty,
                    productName: line.Product?.Name ?? string.Empty,
                    quantity: line.Quantity.ToString(CultureInfo.InvariantCulture),
                    costOrPrice: line.Product?.Price.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty,
                    chefId: line.PreparedByRole == "Chef" ? preparedByEmployee?.UniqueId ?? string.Empty : string.Empty,
                    chefName: line.PreparedByRole == "Chef" ? line.PreparedByName : string.Empty,
                    barmanId: line.PreparedByRole == "Barman" ? preparedByEmployee?.UniqueId ?? string.Empty : string.Empty,
                    barmanName: line.PreparedByRole == "Barman" ? line.PreparedByName : string.Empty));
            }
        }

        if (reportType is "Daily" or "Inventory")
        {
            var inventoryActivityItems = (await _data.GetInventoryItemsAsync().ConfigureAwait(true))
                .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
                .ToList();

            foreach (var item in inventoryActivityItems)
            {
                foreach (var rawLine in item.Notes!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var ts = TryParseLeadingTimestamp(line);
                    if (ts is null || ts.Value < start || ts.Value >= endExclusive)
                        continue;

                    var orderCol = line.Length > 200 ? line[..200] + "…" : line;
                    rows.Add(BuildAnalyticalRow(
                        eventTime: ts.Value,
                        eventType: "Inventory activity",
                        orderId: orderCol,
                        ingredientId: item.UniqueId,
                        ingredientName: item.Name));
                }
            }
        }

        if (includeReservations)
        {
            var endInclusive = endExclusive.AddDays(-1).Date;
            var reservationRows = (await _data.GetReservationsForReportRangeAsync(start.Date, endInclusive).ConfigureAwait(true))
                .OrderBy(r => r.ReservedFor)
                .ThenBy(r => r.Id)
                .ToList();

            foreach (var reservation in reservationRows)
            {
                tablesById.TryGetValue(reservation.TableId ?? 0, out var table);
                var reservationName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                    ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                    : reservation.ReservationName.Trim();

                rows.Add(BuildAnalyticalRow(
                    eventTime: ReservationReportTime.DisplayEventTime(reservation, start, endExclusive),
                    eventType: "Reservation",
                    orderId: reservation.UniqueId,
                    tableId: table?.UniqueId ?? string.Empty,
                    tableName: table is null ? string.Empty : $"Table {table.TableNumber} - {table.Name}",
                    productId: string.Empty,
                    productName: reservationName,
                    quantity: reservation.PartySize.ToString(CultureInfo.InvariantCulture),
                    costOrPrice: reservation.DepositAmountUsd.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        rows = rows
            .OrderByDescending(ParseAnalyticalRowInstant)
            .ThenByDescending(r => r.Count > 3 && ReportDailyTimelineSort.IsPinnedPayrollSalaryEventType(r[3]))
            .ThenBy(r => r[3], StringComparer.Ordinal)
            .ToList();

        return (AnalyticalHeaders, rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildOrderExportRows(
        IReadOnlyList<OrderRecord> orders)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join("; ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, "None", 0m);
            var grandUsd = totals.GrandTotal;
            var payUsd = order.PaymentAmountUsd > 0m
                ? order.PaymentAmountUsd
                : (order.PaymentAmountFc <= 0m ? grandUsd : 0m);
            var payFc = order.PaymentAmountFc;
            var anchor = OrderReportAnchor.Anchor(order);

            rows.Add(
            [
                anchor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                anchor.ToString("dddd", CultureInfo.InvariantCulture),
                anchor.ToString("HH:mm", CultureInfo.InvariantCulture),
                DisplayOrFallback(order.UniqueId, order.Id.ToString(CultureInfo.InvariantCulture)),
                order.Status,
                DisplayOrFallback(order.ServerName, "Unassigned"),
                DisplayOrFallback(order.TableCode, string.Empty),
                DisplayOrFallback(order.TableName, string.Empty),
                menu,
                totalQty.ToString(CultureInfo.InvariantCulture),
                order.Items.Count.ToString(CultureInfo.InvariantCulture),
                payUsd.ToString("0.00", CultureInfo.InvariantCulture),
                payFc.ToString("0.##", CultureInfo.InvariantCulture),
                order.PaymentCurrencyCode
            ]);
        }

        return (OrderReportHeaders, rows);
    }

    private static DateTime ParseAnalyticalRowInstant(IReadOnlyList<string> r)
    {
        if (r.Count < 3)
            return DateTime.MinValue;

        if (DateTime.TryParseExact(
                $"{r[0]} {r[2]}",
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            return dt;

        return DateTime.MinValue;
    }

    private static IReadOnlyList<string> OrderReportHeaders =>
    [
        "Date",
        "Day",
        "Time",
        "Order ID",
        "Status",
        "Server",
        "Table Code",
        "Table Name",
        "Menu (lines)",
        "Item Qty",
        "Line Count",
        "Total USD",
        "Total FC",
        "Pay Currency"
    ];

    private static IReadOnlyList<string> AnalyticalHeaders =>
    [
        "Date",
        "Day",
        "Time",
        "Event Type",
        "Employee ID",
        "Employee Name",
        "Server ID",
        "Server Name",
        "Order ID",
        "Table ID",
        "Table Name",
        "Product ID",
        "Product Name",
        "Qty",
        "Ingredient ID",
        "Ingredient Name",
        "Unit",
        "Cost/Price",
        "Cashier ID",
        "Cashier Name",
        "Chef ID",
        "Chef Name",
        "Barman ID",
        "Barman Name"
    ];

    private static IReadOnlyList<string> BuildAnalyticalRow(
        DateTime eventTime,
        string eventType,
        string employeeId = "",
        string employeeName = "",
        string serverId = "",
        string serverName = "",
        string orderId = "",
        string tableId = "",
        string tableName = "",
        string productId = "",
        string productName = "",
        string quantity = "",
        string ingredientId = "",
        string ingredientName = "",
        string unit = "",
        string costOrPrice = "",
        string cashierId = "",
        string cashierName = "",
        string chefId = "",
        string chefName = "",
        string barmanId = "",
        string barmanName = "")
    {
        return
        [
            eventTime.ToString("yyyy-MM-dd"),
            eventTime.ToString("dddd", CultureInfo.InvariantCulture),
            eventTime.ToString("HH:mm"),
            eventType,
            employeeId,
            employeeName,
            serverId,
            serverName,
            orderId,
            tableId,
            tableName,
            productId,
            productName,
            quantity,
            ingredientId,
            ingredientName,
            unit,
            costOrPrice,
            cashierId,
            cashierName,
            chefId,
            chefName,
            barmanId,
            barmanName
        ];
    }
}
