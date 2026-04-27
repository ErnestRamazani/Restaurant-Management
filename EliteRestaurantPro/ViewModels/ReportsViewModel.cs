using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Utils;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
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
            LoadEmployeeDetails(value?.Id);
        }
    }

    public ReportEntityItem? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (!SetField(ref _selectedTable, value))
                return;
            LoadTableDetails(value?.Id);
        }
    }

    public ReportEntityItem? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            if (!SetField(ref _selectedInventoryItem, value))
                return;
            LoadInventoryDetails(value?.Id);
        }
    }

    public ReportEntityItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            if (!SetField(ref _selectedMenuItem, value))
                return;
            LoadMenuDetails(value?.Id);
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
            LoadDailyReport();
            LoadOrdersReport();
        }
    }

    public DateTime ReportEndDate
    {
        get => _reportEndDate;
        set
        {
            if (!SetField(ref _reportEndDate, value))
                return;
            LoadDailyReport();
            LoadOrdersReport();
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
        RefreshCommand = new RelayCommand(_ => LoadReportLists());
        ExportExcelCommand = new RelayCommand(_ => ExportExcel());
        BulkExportExcelCommand = new RelayCommand(_ => ExportBulkExcel());
        LoadReportLists();
    }

    private void LoadReportLists()
    {
        using var db = new AppDbContext();

        Employees.Clear();
        foreach (var employee in db.Employees.AsNoTracking().OrderBy(e => e.Name))
        {
            Employees.Add(new ReportEntityItem
            {
                Id = employee.Id,
                UniqueId = employee.UniqueId,
                Name = employee.Name,
                Subtitle = employee.Role
            });
        }

        Tables.Clear();
        foreach (var table in db.Tables.AsNoTracking().OrderBy(t => t.TableNumber))
        {
            Tables.Add(new ReportEntityItem
            {
                Id = table.Id,
                UniqueId = table.UniqueId,
                Name = $"Table {table.TableNumber} - {table.Name}",
                Subtitle = table.Status
            });
        }

        InventoryItems.Clear();
        foreach (var item in db.InventoryItems.AsNoTracking().OrderBy(i => i.Name))
        {
            InventoryItems.Add(new ReportEntityItem
            {
                Id = item.Id,
                UniqueId = item.UniqueId,
                Name = item.Name,
                Subtitle = $"{item.StockQuantity:0.##} {item.Unit}"
            });
        }

        MenuItems.Clear();
        foreach (var product in db.Products.AsNoTracking().OrderBy(p => p.Name))
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
        LoadDailyReport();
        LoadOrdersReport();
    }

    private void LoadOrdersReport()
    {
        OrderTimelineDays.Clear();
        var start = ReportStartDate.Date;
        var endExclusive = ReportEndDate.Date.AddDays(1);
        if (endExclusive <= start)
        {
            OrdersSummary = "Set a valid date range (end on or after start).";
            return;
        }

        using var db = new AppDbContext();
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        var entries = new List<ReportTimeEntryDto>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join(", ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, "None", 0m);
            var grandUsd = totals.GrandTotal;
            var payUsd = order.PaymentAmountUsd > 0m ? order.PaymentAmountUsd : grandUsd;
            var payFc = order.PaymentAmountFc > 0m ? order.PaymentAmountFc : CurrencyHelper.ConvertUsdToFc(payUsd);
            var paymentText = CurrencyHelper.FormatDualCurrency(payUsd, payFc);

            entries.Add(new ReportTimeEntryDto
            {
                EventTime = order.CreatedAt,
                EventType = string.IsNullOrWhiteSpace(order.Status) ? "Order" : order.Status,
                Summary = $"Order {DisplayOrFallback(order.UniqueId, $"#{order.Id}")} · {totalQty} item(s)",
                RelatedInfo =
                    $"Server: {DisplayOrFallback(order.ServerName, "Unassigned")} | Table: {DisplayOrFallback(order.TableCode, "?")} · {DisplayOrFallback(order.TableName, "-")} | {DisplayOrFallback(menu, "No line items")}",
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

    private void LoadEmployeeDetails(int? employeeId)
    {
        EmployeeTimelineDays.Clear();
        EmployeeSummary = "Select an employee to view details.";
        EmployeeNotes = "-";
        EmployeePayrollHistory = "-";

        if (employeeId is null)
            return;

        using var db = new AppDbContext();
        var employee = db.Employees.AsNoTracking().SingleOrDefault(e => e.Id == employeeId.Value);
        if (employee is null)
            return;

        EmployeeNotes = string.IsNullOrWhiteSpace(employee.Notes) ? "-" : employee.Notes;

        var payrollLines = db.PayrollPaymentRecords.AsNoTracking()
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
                        $"{PayrollCalculator.FormatPayrollMonthLabel(p.Year, p.Month)}: {paidLine} (base gross ${p.MonthlySalaryUsd:N2}, sales ${p.MoneyGeneratedUsd:N2}, 5% bonus ${p.BonusFivePercentUsd:N2}, advances -${p.AdvancesDeductedUsd:N2}) — last posting {p.PaidAtUtc.ToLocalTime():yyyy-MM-dd}";
                }));
        var entries = new List<ReportTimeEntryDto>();

        var attendanceRows = db.EmployeeAttendances
            .AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.WorkDate)
            .Take(120)
            .ToList();

        foreach (var row in attendanceRows)
        {
            var clockIn = row.ClockInTime?.ToString("HH:mm") ?? "Not clocked in";
            var clockOut = row.ClockOutTime?.ToString("HH:mm") ?? "Not clocked out";
            var statusText = string.IsNullOrWhiteSpace(row.ClockInStatus) ? "Pending" : row.ClockInStatus;
            var noteText = string.IsNullOrWhiteSpace(row.Justification) ? "-" : row.Justification;
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = row.ClockInTime ?? row.WorkDate.Date.AddHours(9),
                EventType = "Attendance",
                Summary = $"Clock In: {clockIn} ({statusText}) | Clock Out: {clockOut}",
                RelatedInfo = $"Justification: {noteText}",
                EntityContext = employee.Name
            });
        }

        var servedOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.ServerId == employee.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(180)
            .ToList();

        foreach (var order in servedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = order.CreatedAt,
                EventType = "Served Order",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"{order.TableCode} ({order.TableName}) | {DisplayOrFallback(items, "No order items.")}",
                EntityContext = employee.Name,
                OrdersCount = 1,
                ItemCount = totalItems
            });
        }

        var empMarker = $"| EMP:{employee.Id}|";
        var salaryMoneyRows = db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.Type == "Expense" &&
                t.Category == "Salary" &&
                t.Justification.Contains(empMarker))
            .OrderByDescending(t => t.Date)
            .Take(48)
            .ToList();

        foreach (var t in salaryMoneyRows)
        {
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = t.Date,
                EventType = t.Justification.Contains("| ADVANCE:", StringComparison.Ordinal)
                    ? "Salary advance (Money)"
                    : "Salary payment (Money)",
                Summary = t.Justification,
                RelatedInfo = $"USD $ {t.AmountUsd:0.00}",
                EntityContext = employee.Name
            });
        }

        var advanceRows = db.SalaryAdvances
            .AsNoTracking()
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
            $"Name: {employee.Name}\nID: {employee.UniqueId}\nRole: {employee.Role}\nStatus: {employee.EmploymentStatus}\nPhone: {employee.PhoneNumber}\nAttendance Events: {attendanceRows.Count}\nOrders Served: {servedOrders.Count}\nItems Served: {servedOrders.Sum(o => o.Items.Sum(i => i.Quantity))}";
    }

    private void LoadTableDetails(int? tableId)
    {
        TableTimelineDays.Clear();
        TableSummary = "Select a table to view details.";
        TableCurrentServer = "-";

        if (tableId is null)
            return;

        using var db = new AppDbContext();
        var table = db.Tables
            .AsNoTracking()
            .Include(t => t.AssignedServer)
            .SingleOrDefault(t => t.Id == tableId.Value);
        if (table is null)
            return;

        var entries = new List<ReportTimeEntryDto>();
        var relatedOrders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.TableId == table.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(220)
            .ToList();

        foreach (var order in relatedOrders)
        {
            var totalItems = order.Items.Sum(i => i.Quantity);
            var items = string.Join(", ", order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = order.CreatedAt,
                EventType = "Table Order",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"Server: {DisplayOrFallback(order.ServerName, "Unassigned")} | {DisplayOrFallback(items, "No order items.")}",
                EntityContext = $"Table {table.TableNumber}",
                OrdersCount = 1,
                ItemCount = totalItems
            });
        }

        var relatedReservations = db.Reservations
            .AsNoTracking()
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
        TableCurrentServer = table.AssignedServer is null
            ? "Unassigned"
            : $"{table.AssignedServer.Name} ({table.AssignedServer.UniqueId})";
    }

    private void LoadInventoryDetails(int? inventoryId)
    {
        InventoryTimelineDays.Clear();
        InventorySummary = "Select an inventory item to view details.";
        InventoryNotes = "-";

        if (inventoryId is null)
            return;

        using var db = new AppDbContext();
        var item = db.InventoryItems.AsNoTracking().SingleOrDefault(i => i.Id == inventoryId.Value);
        if (item is null)
            return;

        InventoryNotes = string.IsNullOrWhiteSpace(item.Notes) ? "-" : item.Notes;

        var ingredients = db.ProductIngredients
            .AsNoTracking()
            .Include(pi => pi.Product)
            .Where(pi => pi.InventoryItemId == item.Id)
            .ToList();

        var entries = new List<ReportTimeEntryDto>();

        AppendInventoryNotesTimeline(item.Notes, entries, item.Name);
        ApplyDayGroups(InventoryTimelineDays, entries);

        InventorySummary =
            $"Item: {item.Name}\nID: {item.UniqueId}\nQuantity: {item.StockQuantity:0.##} {item.Unit}\nExpiration: {item.ExpirationDate?.ToString("yyyy-MM-dd") ?? "Not set"}\nLinked Menu Items: {ingredients.Select(i => i.ProductId).Distinct().Count()}";
    }

    private void LoadMenuDetails(int? productId)
    {
        MenuTimelineDays.Clear();
        MenuSummary = "Select a menu item to view details.";
        MenuIngredientsSummary = "-";

        if (productId is null)
            return;

        using var db = new AppDbContext();
        var product = db.Products.AsNoTracking().SingleOrDefault(p => p.Id == productId.Value);
        if (product is null)
            return;

        var ingredients = db.ProductIngredients
            .AsNoTracking()
            .Include(pi => pi.InventoryItem)
            .Where(pi => pi.ProductId == product.Id)
            .ToList();

        MenuIngredientsSummary = ingredients.Count == 0
            ? "No ingredients linked."
            : string.Join(", ", ingredients.Select(i => $"{i.InventoryItem?.Name ?? "Unknown"} ({i.Quantity:0.##} {i.InventoryItem?.Unit ?? "unit"})"));

        var entries = new List<ReportTimeEntryDto>();
        var servedLines = db.OrderItems
            .AsNoTracking()
            .Include(oi => oi.OrderRecord!)
            .ThenInclude(o => o.Table)
            .Where(oi => oi.ProductId == product.Id)
            .OrderByDescending(oi => oi.OrderRecord!.CreatedAt)
            .Take(300)
            .ToList();

        foreach (var line in servedLines)
        {
            var order = line.OrderRecord;
            if (order is null)
                continue;

            entries.Add(new ReportTimeEntryDto
            {
                EventTime = order.CreatedAt,
                EventType = "Menu Ordered",
                Summary = $"{product.Name} x{line.Quantity} in order {order.UniqueId}",
                RelatedInfo = $"{order.TableCode} ({order.TableName}) | Server: {DisplayOrFallback(order.ServerName, "Unassigned")} | Status: {order.Status}",
                EntityContext = product.Name,
                OrdersCount = 1,
                ItemCount = line.Quantity
            });
        }

        ApplyDayGroups(MenuTimelineDays, entries);

        var totalQty = servedLines.Sum(l => l.Quantity);
        MenuSummary =
            $"Menu Item: {product.Name}\nID: {product.UniqueId}\nCategory: {product.Category}\nSub Category: {product.SubCategory}\nPrice: $ {product.Price:0.00}\nTimes Ordered: {servedLines.Count}\nUnits Ordered: {totalQty}";
    }

    private void LoadDailyReport()
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

        using var db = new AppDbContext();
        var entries = new List<ReportTimeEntryDto>();

        var attendanceRows = db.EmployeeAttendances
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.WorkDate >= rangeStartUtc && a.WorkDate < rangeEndExclusiveUtc)
            .OrderByDescending(a => a.WorkDate)
            .ToList();

        foreach (var row in attendanceRows)
        {
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = row.ClockInTime ?? row.WorkDate.Date.AddHours(9),
                EventType = "Attendance",
                Summary = $"{row.Employee?.Name ?? "Unknown"} clocked {(row.ClockInTime.HasValue ? "in" : "scheduled")} ({DisplayOrFallback(row.ClockInStatus, "Pending")})",
                RelatedInfo = $"Clock Out: {row.ClockOutTime?.ToString("HH:mm") ?? "Not clocked out"} | Justification: {DisplayOrFallback(row.Justification, "-")}",
                EntityContext = $"Employee: {row.Employee?.Name ?? "Unknown"}"
            });
        }

        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        foreach (var order in orders)
        {
            var itemQty = order.Items.Sum(i => i.Quantity);
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = order.CreatedAt,
                EventType = "Table Service",
                Summary = $"Order {order.UniqueId} ({order.Status})",
                RelatedInfo = $"{order.TableCode} ({order.TableName}) | Server: {DisplayOrFallback(order.ServerName, "Unassigned")} | Items: {itemQty}",
                EntityContext = $"Table: {order.TableCode}",
                OrdersCount = 1,
                ItemCount = itemQty
            });
        }

        var reservations = db.Reservations
            .AsNoTracking()
            .Include(r => r.Table)
            .Where(r => r.UpdatedAt >= start && r.UpdatedAt < endExclusive)
            .OrderByDescending(r => r.UpdatedAt)
            .ToList();

        foreach (var reservation in reservations)
        {
            var displayName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                : reservation.ReservationName.Trim();
            var tableLabel = reservation.Table is not null
                ? $"{reservation.Table.TableNumber} ({DisplayOrFallback(reservation.Table.Name, "-")})"
                : "-";
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = reservation.UpdatedAt,
                EventType = "Reservation",
                Summary = $"Reservation {reservation.UniqueId} · {displayName} · {reservation.Status}",
                RelatedInfo = $"Reserved for {reservation.ReservedFor:yyyy-MM-dd HH:mm} | Party: {reservation.PartySize} | Table: {tableLabel}",
                EntityContext = "Reservations"
            });
        }

        var menuLines = db.OrderItems
            .AsNoTracking()
            .Include(oi => oi.Product)
            .Include(oi => oi.OrderRecord)
            .Where(oi => oi.OrderRecord != null &&
                         oi.OrderRecord.CreatedAt >= start &&
                         oi.OrderRecord.CreatedAt < endExclusive)
            .OrderByDescending(oi => oi.OrderRecord!.CreatedAt)
            .ToList();

        foreach (var line in menuLines)
        {
            if (line.OrderRecord is null)
                continue;

            entries.Add(new ReportTimeEntryDto
            {
                EventTime = line.OrderRecord.CreatedAt,
                EventType = "Menu Activity",
                Summary = $"{line.Product?.Name ?? "Unknown"} x{line.Quantity}",
                RelatedInfo = $"Order {line.OrderRecord.UniqueId} | {line.OrderRecord.TableCode} ({line.OrderRecord.TableName})",
                EntityContext = $"Menu: {line.Product?.Name ?? "Unknown"}",
                OrdersCount = 1,
                ItemCount = line.Quantity
            });
        }

        var inventoryNoteRows = db.InventoryItems
            .AsNoTracking()
            .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
            .Select(i => new InventoryNotesSnapshot(i.UniqueId, i.Name, i.Notes))
            .ToList();
        AppendInventoryActivityFromNotes(entries, start, endExclusive, inventoryNoteRows);

        var salaryTx = db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.Type == "Expense" &&
                t.Category == "Salary" &&
                t.Date >= start &&
                t.Date < endExclusive)
            .OrderByDescending(t => t.Date)
            .ToList();

        foreach (var t in salaryTx)
        {
            entries.Add(new ReportTimeEntryDto
            {
                EventTime = t.Date,
                EventType = "Salary / Money",
                Summary = t.Justification,
                RelatedInfo = $"USD $ {t.AmountUsd:0.00} (same as Money ledger)",
                EntityContext = "Money"
            });
        }

        ApplyDayGroups(DailyTimelineDays, entries);
        DailySummary =
            $"Daily timeline {start:yyyy-MM-dd} → {ReportEndDate:yyyy-MM-dd}: {entries.Count} events (attendance, orders, reservations, menu, inventory activity, salary/Money).";
    }

    private static void ApplyDayGroups(ObservableCollection<ReportDayGroupDto> target, IEnumerable<ReportTimeEntryDto> entries)
    {
        var normalized = entries
            .OrderByDescending(e => e.EventTime)
            .ThenBy(e => e.EventType)
            .ToList();

        target.Clear();
        foreach (var dayGroup in normalized.GroupBy(e => e.EventTime.Date))
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

    private void ExportExcel()
    {
        var range = TryGetExportRange();
        if (range is null)
            return;

        if (SelectedExportReportType == "All Reports")
        {
            ExportBulkExcel();
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

        var data = BuildExportRows(SelectedExportReportType, range.Value.Start, range.Value.EndExclusive);
        ExcelExportService.ExportSingleSheet(saveDialog.FileName, SelectedExportReportType, data.Headers, data.Rows);
    }

    private void ExportBulkExcel()
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

        var daily = BuildExportRows("Daily", range.Value.Start, range.Value.EndExclusive);
        var orders = BuildExportRows("Orders", range.Value.Start, range.Value.EndExclusive);
        var employees = BuildExportRows("Employees", range.Value.Start, range.Value.EndExclusive);
        var tables = BuildExportRows("Tables", range.Value.Start, range.Value.EndExclusive);
        var inventory = BuildExportRows("Inventory", range.Value.Start, range.Value.EndExclusive);
        var menu = BuildExportRows("Menu", range.Value.Start, range.Value.EndExclusive);

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

    private (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildExportRows(string reportType, DateTime start, DateTime endExclusive)
    {
        using var db = new AppDbContext();

        if (reportType == "Orders")
            return BuildOrderExportRows(db, start, endExclusive);

        var employeesById = db.Employees
            .AsNoTracking()
            .ToDictionary(e => e.Id, e => e);
        var tablesById = db.Tables
            .AsNoTracking()
            .ToDictionary(t => t.Id, t => t);

        var rows = new List<IReadOnlyList<string>>();

        bool includeAttendance = reportType is "Daily" or "Employees";
        bool includeOrders = reportType is "Daily" or "Employees" or "Tables" or "Menu";
        bool includeReservations = reportType is "Daily" or "Tables";

        if (includeAttendance)
        {
            var attendanceStartUtc = AttendanceCalendar.DayAnchorUtc(start.Date);
            var attendanceEndExclusiveUtc = AttendanceCalendar.DayAnchorUtc(endExclusive.Date);
            var attendanceRows = db.EmployeeAttendances
                .AsNoTracking()
                .Include(a => a.Employee)
                .Where(a => a.WorkDate >= attendanceStartUtc && a.WorkDate < attendanceEndExclusiveUtc)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => a.EmployeeId)
                .ToList();

            foreach (var row in attendanceRows)
            {
                rows.Add(BuildAnalyticalRow(
                    eventTime: row.ClockInTime ?? row.WorkDate.Date,
                    eventType: "Attendance",
                    employeeId: row.Employee?.UniqueId ?? string.Empty,
                    employeeName: row.Employee?.Name ?? string.Empty,
                    costOrPrice: string.Empty));
            }
        }

        var includeSalaryLedger = reportType is "Daily" or "Employees";
        if (includeSalaryLedger)
        {
            var salaryTx = db.Transactions
                .AsNoTracking()
                .Where(t =>
                    t.Type == "Expense" &&
                    t.Category == "Salary" &&
                    t.Date >= start &&
                    t.Date < endExclusive)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .ToList();

            foreach (var t in salaryTx)
            {
                _ = TryParseEmployeeIdFromSalaryJustification(t.Justification, out var eid);
                employeesById.TryGetValue(eid, out var empRef);
                var j = t.Justification;
                if (j.Length > 120)
                    j = j[..120] + "…";

                rows.Add(BuildAnalyticalRow(
                    eventTime: t.Date,
                    eventType: t.Justification.Contains("| ADVANCE:", StringComparison.Ordinal)
                        ? "Salary advance (Money)"
                        : "Salary payment (Money)",
                    employeeId: empRef?.UniqueId ?? string.Empty,
                    employeeName: empRef?.Name ?? string.Empty,
                    orderId: j,
                    costOrPrice: t.AmountUsd.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        var orderItems = new List<OrderItem>();
        if (includeOrders)
        {
            orderItems = db.OrderItems
                .AsNoTracking()
                .Include(oi => oi.OrderRecord)
                .Include(oi => oi.Product)
                .Where(oi => oi.OrderRecord != null &&
                             oi.OrderRecord.CreatedAt >= start &&
                             oi.OrderRecord.CreatedAt < endExclusive)
                .OrderBy(oi => oi.OrderRecord!.CreatedAt)
                .ThenBy(oi => oi.Id)
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

                rows.Add(BuildAnalyticalRow(
                    eventTime: order.CreatedAt,
                    eventType: "Order",
                    employeeId: preparedByEmployee?.UniqueId ?? string.Empty,
                    employeeName: line.PreparedByName,
                    serverId: serverEmployee?.UniqueId ?? string.Empty,
                    serverName: order.ServerName,
                    orderId: order.UniqueId,
                    tableId: table?.UniqueId ?? string.Empty,
                    tableName: order.TableName,
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
            var inventoryActivityItems = db.InventoryItems
                .AsNoTracking()
                .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
                .ToList();

            foreach (var item in inventoryActivityItems)
            {
                foreach (var rawLine in item.Notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
            var reservationRows = db.Reservations
                .AsNoTracking()
                .Where(r => r.UpdatedAt >= start && r.UpdatedAt < endExclusive)
                .OrderBy(r => r.UpdatedAt)
                .ThenBy(r => r.Id)
                .ToList();

            foreach (var reservation in reservationRows)
            {
                tablesById.TryGetValue(reservation.TableId ?? 0, out var table);
                var reservationName = string.IsNullOrWhiteSpace(reservation.ReservationName)
                    ? DisplayOrFallback(reservation.GuestName, reservation.UniqueId)
                    : reservation.ReservationName.Trim();

                rows.Add(BuildAnalyticalRow(
                    eventTime: reservation.UpdatedAt,
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
            .OrderBy(r => r[0])
            .ThenBy(r => r[2])
            .ThenBy(r => r[3])
            .ToList();

        return (AnalyticalHeaders, rows);
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildOrderExportRows(
        AppDbContext db,
        DateTime start,
        DateTime endExclusive)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
            .OrderBy(o => o.CreatedAt)
            .ToList();

        var rows = new List<IReadOnlyList<string>>();
        foreach (var order in orders)
        {
            var totalQty = order.Items.Sum(i => i.Quantity);
            var menu = string.Join("; ",
                order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} ×{i.Quantity}"));
            var subtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(subtotal, "None", 0m);
            var grandUsd = totals.GrandTotal;
            var payUsd = order.PaymentAmountUsd > 0m ? order.PaymentAmountUsd : grandUsd;
            var payFc = order.PaymentAmountFc > 0m ? order.PaymentAmountFc : CurrencyHelper.ConvertUsdToFc(payUsd);

            rows.Add(
            [
                order.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                order.CreatedAt.ToString("dddd", CultureInfo.InvariantCulture),
                order.CreatedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
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

    private static bool TryParseEmployeeIdFromSalaryJustification(string justification, out int employeeId)
    {
        employeeId = 0;
        if (string.IsNullOrEmpty(justification))
            return false;

        const string marker = "| EMP:";
        var idx = justification.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        var from = idx + marker.Length;
        var end = justification.IndexOf('|', from);
        var slice = end >= 0 ? justification.Substring(from, end - from) : justification.Substring(from);
        return int.TryParse(slice, NumberStyles.Integer, CultureInfo.InvariantCulture, out employeeId);
    }

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
