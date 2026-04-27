using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public sealed class DashboardAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool IsCritical { get; init; }
}

public sealed class DashboardTopDishItem
{
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public double BarPercent { get; init; }
    public double BarWidthPx => Math.Min(200, Math.Max(4, BarPercent * 2));
}

public sealed class DashboardStaffPresenceItem
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsClockedIn { get; init; }
    public string OnDutyLabel => IsClockedIn ? "Active" : "Off duty";
}

public class AdminDashboardViewModel : AdminBaseViewModel
{
    private readonly Dictionary<DashboardDrilldownType, List<DashboardDrilldownItem>> _drilldownCache = [];
    private bool _isNavigatingToShortcut;
    private DispatcherTimer? _clockTimer;

    public override string ActivePage => "Dashboard";

    public string WelcomeName { get; } = "Ernest";

    public string TodayRevenueUsd { get; private set; } = CurrencyHelper.FormatAmount(0m, CurrencyHelper.Usd);
    public string TodayRevenueFc { get; private set; } = CurrencyHelper.FormatAmount(0m, CurrencyHelper.CongoleseFranc);
    public string TodayExpensesUsd { get; private set; } = CurrencyHelper.FormatAmount(0m, CurrencyHelper.Usd);
    public string RevenueUsdVsYesterdayText { get; private set; } = string.Empty;
    public string RevenueFcVsYesterdayText { get; private set; } = string.Empty;
    public string ExpensesVsYesterdayText { get; private set; } = string.Empty;
    public string SparklinePoints { get; private set; } = "0,24 80,24";

    public int OrdersCompletedToday { get; private set; }
    public string CompletedVsYesterdayText { get; private set; } = string.Empty;

    public int ActiveOrdersCount { get; private set; }
    public string ClockedInStaff { get; private set; } = "0/0";
    public string InKitchenNowText { get; private set; } = "0 in kitchen now";
    public string NotYetInText { get; private set; } = "0 not yet in";
    public string OccupiedTablesText { get; private set; } = "0/0 occupied";
    public double OccupiedTablesPercent { get; private set; }

    public bool DatabaseConnected { get; private set; } = true;
    public string DatabaseStatusText { get; private set; } = "Database: Connected";

    public string LiveClockText { get; private set; } = string.Empty;

    public bool ShowSalaryPayrollWarning { get; private set; }
    public string SalaryPayrollWarningText { get; private set; } = string.Empty;
    public int SalaryPayrollDaysPastDue { get; private set; }

    public string ChartAreaPoints { get; private set; } = "0,160 145,160 290,160 435,160 580,160 580,160 0,160";
    public string ChartLinePoints { get; private set; } = "0,160 145,160 290,160 435,160 580,160";
    public string WeeklyChartMaxLabel { get; private set; } = "$1000";

    public string HourlyChartAreaPoints { get; private set; } = string.Empty;
    public string HourlyChartLinePoints { get; private set; } = string.Empty;
    public string HourlyChartMaxLabel { get; private set; } = "$0";

    public ObservableCollection<DashboardAlertItem> CriticalInventoryAlerts { get; } = [];
    public ObservableCollection<DashboardAlertItem> ReorderInventoryAlerts { get; } = [];
    public ObservableCollection<DashboardTopDishItem> TopSellingDishes { get; } = [];
    public ObservableCollection<DashboardStaffPresenceItem> StaffPresence { get; } = [];
    public ObservableCollection<ActivityItem> RecentActivities { get; } = [];

    public ICommand OpenTodaySalesCommand { get; }
    public ICommand OpenActiveOrdersCommand { get; }
    public ICommand OpenLowStockAlertsCommand { get; }
    public ICommand OpenClockedInStaffCommand { get; }
    public ICommand OpenWeeklyRevenueCommand { get; }
    public ICommand OpenRecentActivityCommand { get; }
    public ICommand OpenActivityItemCommand { get; }

    public AdminDashboardViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenTodaySalesCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.TodaySales));
        OpenActiveOrdersCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.ActiveOrders));
        OpenLowStockAlertsCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.LowStockAlerts));
        OpenClockedInStaffCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.ClockedInStaff));
        OpenWeeklyRevenueCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.WeeklyRevenue));
        OpenRecentActivityCommand = new RelayCommand(_ => OpenDrilldown(DashboardDrilldownType.RecentActivity));
        OpenActivityItemCommand = new RelayCommand(activity => OpenFromActivity(activity as ActivityItem));

        UpdateLiveClock();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clockTimer.Tick += (_, _) =>
        {
            UpdateLiveClock();
            OnPropertyChanged(nameof(LiveClockText));
        };
        _clockTimer.Start();

        _ = LoadDashboardDataAsync();
    }

    private void UpdateLiveClock()
        => LiveClockText = DateTime.Now.ToString("dddd, MMMM dd, yyyy  ·  HH:mm", CultureInfo.InvariantCulture);

    private void OpenDrilldown(DashboardDrilldownType type)
    {
        if (_isNavigatingToShortcut)
            return;

        var title = type switch
        {
            DashboardDrilldownType.TodaySales => "Today's Sales",
            DashboardDrilldownType.ActiveOrders => "Active Orders",
            DashboardDrilldownType.LowStockAlerts => "Low Stock Alerts",
            DashboardDrilldownType.ClockedInStaff => "Clocked-In Staff",
            DashboardDrilldownType.WeeklyRevenue => "Weekly Revenue",
            _ => "Recent Activity"
        };

        var subtitle = type switch
        {
            DashboardDrilldownType.TodaySales => "All non-cancelled orders created today.",
            DashboardDrilldownType.ActiveOrders => "Orders currently in service flow.",
            DashboardDrilldownType.LowStockAlerts => "Inventory items with quantity 10 or below.",
            DashboardDrilldownType.ClockedInStaff => "Today attendance with clock-in information.",
            DashboardDrilldownType.WeeklyRevenue => "Revenue grouped by day for current work week.",
            _ => $"Last {DashboardDrilldownViewModel.RecentActivityMaxItems} operational events from orders, team attendance, and stock notes."
        };

        _drilldownCache.TryGetValue(type, out var items);
        var snapshotItems = items?.Select(i => new DashboardDrilldownItem
        {
            Title = i.Title,
            Subtitle = i.Subtitle,
            Detail = i.Detail,
            Meta = i.Meta,
            AccentColor = i.AccentColor
        }).ToList();
        _isNavigatingToShortcut = true;
        try
        {
            NavigateAction(new DashboardDrilldownViewModel(NavigateAction, title, subtitle, snapshotItems));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open dashboard details.\n\n{ex.Message}",
                "Dashboard",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isNavigatingToShortcut = false;
        }
    }

    private void OpenFromActivity(ActivityItem? activity)
    {
        if (activity is null)
        {
            OpenDrilldown(DashboardDrilldownType.RecentActivity);
            return;
        }

        switch (activity.NavigationTarget)
        {
            case DashboardActivityNav.Orders:
                NavigateAction(new AdminOrdersViewModel(NavigateAction));
                return;
            case DashboardActivityNav.Attendance:
                NavigateAction(new AttendanceViewModel(NavigateAction));
                return;
            case DashboardActivityNav.Inventory:
                NavigateAction(new InventoryViewModel(NavigateAction));
                return;
            case DashboardActivityNav.Money:
                NavigateAction(new MoneyViewModel(NavigateAction));
                return;
        }

        if (activity.Title.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase) ||
            activity.Title.StartsWith("Order", StringComparison.OrdinalIgnoreCase))
        {
            NavigateAction(new AdminOrdersViewModel(NavigateAction));
        }
        else if (activity.Description.Contains("Clocked", StringComparison.OrdinalIgnoreCase))
        {
            NavigateAction(new AttendanceViewModel(NavigateAction));
        }
        else
        {
            OpenDrilldown(DashboardDrilldownType.RecentActivity);
        }
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            var snapshot = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                var dbOk = true;
                try
                {
                    db.Database.CanConnect();
                }
                catch
                {
                    dbOk = false;
                }

                var today = DateTime.Today;
                var (attendanceTodayStartUtc, attendanceTodayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);
                var tomorrow = today.AddDays(1);
                var yesterday = today.AddDays(-1);
                var activeStatuses = new[]
                {
                    "Waiting", "In Kitchen", "Ready", OrderWorkflow.Served, OrderWorkflow.PendingCashier
                };

                var allOrders = db.Orders.AsNoTracking().ToList();
                var todaysOrders = allOrders
                    .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();
                var yesterdaysOrders = allOrders
                    .Where(o => o.CreatedAt >= yesterday && o.CreatedAt < today && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var products = db.Products.AsNoTracking().ToList();
                var orderItems = db.OrderItems.AsNoTracking().ToList();
                var productPriceById = products.ToDictionary(p => p.Id, p => p.Price);

                var orderTotals = orderItems
                    .GroupBy(item => item.OrderRecordId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(item => (productPriceById.TryGetValue(item.ProductId, out var price) ? price : 0m) * item.Quantity));

                static decimal GrandTotalForOrder(OrderRecord order, decimal lineItemsSubtotal)
                    => OrderTotalsHelper.ComputeTotals(lineItemsSubtotal, order.DiscountMode, order.DiscountValue).GrandTotal;

                static bool IsCompleted(OrderRecord o)
                    => string.Equals(o.Status, "Completed", StringComparison.OrdinalIgnoreCase);

                MoneyDashboardTotals.EnsureSaleRevenueBackfill(db);
                var moneyWindowStart = today.AddDays(-6);
                var txMoneyWindow = db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Date >= moneyWindowStart && t.Date < tomorrow)
                    .ToList();
                var txToday = txMoneyWindow.Where(t => t.Date >= today).ToList();
                var txYesterday = txMoneyWindow.Where(t => t.Date >= yesterday && t.Date < today).ToList();

                var todayRevenueUsdValue = MoneyDashboardTotals.SumRevenueByCurrency(txToday, CurrencyHelper.Usd);
                var todayRevenueFcValue = MoneyDashboardTotals.SumRevenueByCurrency(txToday, CurrencyHelper.CongoleseFranc);
                var todayExpensesUsdValue = MoneyDashboardTotals.SumExpensesUsd(txToday);

                var yesterdayRevenueUsdValue = MoneyDashboardTotals.SumRevenueByCurrency(txYesterday, CurrencyHelper.Usd);
                var yesterdayRevenueFcValue = MoneyDashboardTotals.SumRevenueByCurrency(txYesterday, CurrencyHelper.CongoleseFranc);
                var yesterdayExpensesUsdValue = MoneyDashboardTotals.SumExpensesUsd(txYesterday);

                var revenueUsdDelta = yesterdayRevenueUsdValue > 0m
                    ? (todayRevenueUsdValue - yesterdayRevenueUsdValue) / yesterdayRevenueUsdValue * 100m
                    : todayRevenueUsdValue > 0m ? 100m : 0m;
                var revenueFcDelta = yesterdayRevenueFcValue > 0m
                    ? (todayRevenueFcValue - yesterdayRevenueFcValue) / yesterdayRevenueFcValue * 100m
                    : todayRevenueFcValue > 0m ? 100m : 0m;
                var expensesUsdDelta = yesterdayExpensesUsdValue > 0m
                    ? (todayExpensesUsdValue - yesterdayExpensesUsdValue) / yesterdayExpensesUsdValue * 100m
                    : todayExpensesUsdValue > 0m ? 100m : 0m;

                var todaySalesValue = todaysOrders.Sum(order => GrandTotalForOrder(order, orderTotals.TryGetValue(order.Id, out var total) ? total : 0m));
                var yesterdaySalesValue = yesterdaysOrders.Sum(order => GrandTotalForOrder(order, orderTotals.TryGetValue(order.Id, out var total) ? total : 0m));

                var completedToday = todaysOrders.Where(IsCompleted).ToList();
                var completedYesterday = yesterdaysOrders.Where(IsCompleted).ToList();
                var ordersCompletedToday = completedToday.Count;
                var ordersCompletedYesterday = completedYesterday.Count;

                var completedDelta = ordersCompletedYesterday > 0
                    ? (ordersCompletedToday - ordersCompletedYesterday) / (decimal)ordersCompletedYesterday * 100m
                    : ordersCompletedToday > 0 ? 100m : 0m;

                var activeOrdersCount = allOrders.Count(o => activeStatuses.Contains(o.Status));
                var inKitchenCount = allOrders.Count(o => string.Equals(o.Status, "In Kitchen", StringComparison.OrdinalIgnoreCase));
                var lowStockAlerts = db.InventoryItems.AsNoTracking().Count(i => i.StockQuantity <= 10);
                var totalActiveEmployees = db.Employees.AsNoTracking().Count(e => e.EmploymentStatus == "Active");
                var clockedInCount = db.EmployeeAttendances.AsNoTracking().Count(a =>
                    a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc && a.ClockInTime != null);
                var occupiedTables = db.Tables.AsNoTracking().Count(t => t.Status == "Occupied");
                var totalTables = db.Tables.AsNoTracking().Count();

                var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                var weekDays = Enumerable.Range(0, 5).Select(i => weekStart.AddDays(i)).ToList();
                var revenueByDay = weekDays
                    .Select(day => allOrders
                        .Where(o => o.CreatedAt.Date == day.Date && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                        .Sum(o => GrandTotalForOrder(o, orderTotals.TryGetValue(o.Id, out var total) ? total : 0m)))
                    .ToList();

                var last7Days = Enumerable.Range(-6, 7).Select(i => today.AddDays(i)).ToList();
                var revenueLast7 = last7Days
                    .Select(day =>
                    {
                        var d0 = day.Date;
                        var d1 = d0.AddDays(1);
                        return MoneyDashboardTotals.SumRevenueByCurrency(
                            txMoneyWindow.Where(t => t.Date >= d0 && t.Date < d1),
                            CurrencyHelper.Usd);
                    })
                    .ToList();

                var hourlySales = new decimal[24];
                foreach (var order in todaysOrders)
                {
                    var hour = order.CreatedAt.Hour;
                    hourlySales[hour] += GrandTotalForOrder(order, orderTotals.TryGetValue(order.Id, out var t) ? t : 0m);
                }

                var productNameById = products.ToDictionary(p => p.Id, p => p.Name);
                var topDishes = orderItems
                    .Where(i => todaysOrders.Any(o => o.Id == i.OrderRecordId))
                    .GroupBy(i => i.ProductId)
                    .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                    .OrderByDescending(x => x.Qty)
                    .Take(5)
                    .ToList();
                var maxDishQty = topDishes.Count == 0 ? 1 : topDishes.Max(d => d.Qty);

                var inventoryItems = db.InventoryItems.AsNoTracking().OrderBy(i => i.Name).ToList();
                var criticalAlerts = new List<DashboardAlertItem>();
                var reorderAlerts = new List<DashboardAlertItem>();
                foreach (var item in inventoryItems)
                {
                    var status = item.ExpirationStatus;
                    var critical = status is "Expired" or "Critical" || item.StockQuantity <= 3m;
                    var reorder = !critical && (status == "Bad" || (item.StockQuantity > 3m && item.StockQuantity <= 10m));

                    if (critical)
                    {
                        criticalAlerts.Add(new DashboardAlertItem
                        {
                            Title = item.Name,
                            Detail = $"{item.StockQuantity:0.##} {item.Unit} · {status}" +
                                     (item.ExpirationDate.HasValue ? $" · Expires {item.ExpirationDate:yyyy-MM-dd}" : string.Empty),
                            IsCritical = true
                        });
                    }
                    else if (reorder)
                    {
                        reorderAlerts.Add(new DashboardAlertItem
                        {
                            Title = item.Name,
                            Detail = $"{item.StockQuantity:0.##} {item.Unit} · {status}" +
                                     (item.ExpirationDate.HasValue ? $" · Expires {item.ExpirationDate:yyyy-MM-dd}" : string.Empty),
                            IsCritical = false
                        });
                    }
                }

                var todayAttendance = db.EmployeeAttendances
                    .AsNoTracking()
                    .Where(a => a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc)
                    .ToDictionary(a => a.EmployeeId, a => a);

                var staffPresence = db.Employees
                    .AsNoTracking()
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.Name)
                    .ToList()
                    .Select(emp => new DashboardStaffPresenceItem
                    {
                        Name = emp.Name,
                        Role = emp.Role,
                        IsClockedIn = todayAttendance.TryGetValue(emp.Id, out var a) && a.ClockInTime != null
                    })
                    .ToList();

                List<ActivityItem> activities;
                try
                {
                    activities = DashboardDrilldownViewModel.BuildRecentActivities(db);
                }
                catch
                {
                    activities = [];
                }

                var orderItemsByOrder = orderItems
                    .GroupBy(i => i.OrderRecordId)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(i => i.Quantity)
                            .Take(4)
                            .Select(i => new { i.ProductId, i.Quantity })
                            .ToList());

                var salesItems = todaysOrders
                    .Select(order => new DashboardDrilldownItem
                    {
                        Title = string.IsNullOrWhiteSpace(order.UniqueId) ? $"Order #{order.Id:000}" : order.UniqueId,
                        Subtitle = $"{order.TableCode} · {order.TableName}",
                        Detail = $"Status: {order.Status} · {order.CreatedAt:HH:mm}",
                        Meta = $"$ {GrandTotalForOrder(order, orderTotals.TryGetValue(order.Id, out var total) ? total : 0m):N2}",
                        AccentColor = "#2196F3"
                    })
                    .ToList();

                var activeOrderItems = allOrders
                    .Where(o => activeStatuses.Contains(o.Status))
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(order => new DashboardDrilldownItem
                    {
                        Title = string.IsNullOrWhiteSpace(order.UniqueId) ? $"Order #{order.Id:000}" : order.UniqueId,
                        Subtitle = $"{order.TableCode} · {order.TableName}",
                        Detail = $"Server: {order.ServerName} · {order.Status}",
                        Meta = orderItemsByOrder.TryGetValue(order.Id, out var lines)
                            ? string.Join(", ", lines.Select(line => $"{(productNameById.TryGetValue(line.ProductId, out var name) ? name : "Unknown")} x{line.Quantity}"))
                            : "No items",
                        AccentColor = "#4CAF50"
                    })
                    .ToList();

                var lowStockItems = db.InventoryItems
                    .AsNoTracking()
                    .Where(i => i.StockQuantity <= 10)
                    .ToList()
                    .OrderBy(i => i.StockQuantity)
                    .ThenBy(i => i.Name)
                    .Select(item => new DashboardDrilldownItem
                    {
                        Title = item.Name,
                        Subtitle = item.UniqueId,
                        Detail = $"Stock: {item.StockQuantity:0.##} {item.Unit}",
                        Meta = item.ExpirationDate.HasValue ? $"Expiry: {item.ExpirationDate:yyyy-MM-dd}" : "No expiry date",
                        AccentColor = "#FF9800"
                    })
                    .ToList();

                var attendanceItems = db.EmployeeAttendances
                    .AsNoTracking()
                    .Include(a => a.Employee)
                    .Where(a =>
                        a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc && a.ClockInTime != null)
                    .OrderBy(a => a.ClockInTime)
                    .Select(row => new DashboardDrilldownItem
                    {
                        Title = row.Employee != null ? row.Employee.Name : "Unknown Employee",
                        Subtitle = row.Employee != null ? row.Employee.UniqueId : string.Empty,
                        Detail = $"Clock In: {row.ClockInTime:HH:mm} · Clock Out: {(row.ClockOutTime.HasValue ? row.ClockOutTime.Value.ToString("HH:mm") : "Not clocked out")}",
                        Meta = string.IsNullOrWhiteSpace(row.ClockInStatus) ? "Status: Pending" : $"Status: {row.ClockInStatus}",
                        AccentColor = row.ClockInStatus == "Late" ? "#F44336" : "#2196F3"
                    })
                    .ToList();

                var revenueItems = weekDays
                    .Select(day => new DashboardDrilldownItem
                    {
                        Title = day.ToString("dddd"),
                        Subtitle = day.ToString("yyyy-MM-dd"),
                        Detail = "Revenue",
                        Meta = $"$ {allOrders.Where(o => o.CreatedAt.Date == day.Date && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).Sum(o => GrandTotalForOrder(o, orderTotals.TryGetValue(o.Id, out var total) ? total : 0m)):N2}",
                        AccentColor = "#2196F3"
                    })
                    .ToList();

                var activityItems = activities
                    .Select(activity => new DashboardDrilldownItem
                    {
                        Title = activity.Title,
                        Subtitle = activity.KindLabel,
                        Detail = activity.DetailBlock,
                        Meta = string.Empty,
                        AccentColor = "#D4AF37"
                    })
                    .ToList();

                var topDishVms = topDishes
                    .Select(d => new DashboardTopDishItem
                    {
                        Name = productNameById.TryGetValue(d.ProductId, out var n) ? n : "Unknown",
                        Quantity = d.Qty,
                        BarPercent = maxDishQty > 0 ? d.Qty * 100.0 / maxDishQty : 0
                    })
                    .ToList();

                var salaryOverdue = FinancialTransactionService.GetSalaryOverdueState(db, DateTime.Now);

                return new
                {
                    dbOk,
                    todaySalesValue,
                    yesterdaySalesValue,
                    todayRevenueUsdValue,
                    yesterdayRevenueUsdValue,
                    todayRevenueFcValue,
                    yesterdayRevenueFcValue,
                    todayExpensesUsdValue,
                    yesterdayExpensesUsdValue,
                    revenueUsdDelta,
                    revenueFcDelta,
                    expensesUsdDelta,
                    ordersCompletedToday,
                    ordersCompletedYesterday,
                    completedDelta,
                    activeOrdersCount,
                    inKitchenCount,
                    lowStockAlerts,
                    totalActiveEmployees,
                    clockedInCount,
                    occupiedTables,
                    totalTables,
                    revenueByDay,
                    revenueLast7,
                    hourlySales,
                    activities,
                    salesItems,
                    activeOrderItems,
                    lowStockItems,
                    attendanceItems,
                    revenueItems,
                    activityItems,
                    criticalAlerts,
                    reorderAlerts,
                    staffPresence,
                    topDishVms,
                    salaryWarningShow = salaryOverdue.ShowWarning,
                    salaryWarningMessage = salaryOverdue.Message,
                    salaryDaysPast = salaryOverdue.DaysPastPayDay
                };
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DatabaseConnected = snapshot.dbOk;
                DatabaseStatusText = snapshot.dbOk ? "PostgreSQL · Connected" : "PostgreSQL · Offline";

                ShowSalaryPayrollWarning = snapshot.salaryWarningShow;
                SalaryPayrollWarningText = snapshot.salaryWarningMessage;
                SalaryPayrollDaysPastDue = snapshot.salaryDaysPast;

                TodayRevenueUsd = CurrencyHelper.FormatAmount(snapshot.todayRevenueUsdValue, CurrencyHelper.Usd);
                TodayRevenueFc = CurrencyHelper.FormatAmount(snapshot.todayRevenueFcValue, CurrencyHelper.CongoleseFranc);
                TodayExpensesUsd = CurrencyHelper.FormatAmount(snapshot.todayExpensesUsdValue, CurrencyHelper.Usd);
                RevenueUsdVsYesterdayText = snapshot.yesterdayRevenueUsdValue <= 0m && snapshot.todayRevenueUsdValue <= 0m
                    ? "No USD revenue yesterday to compare"
                    : $"{snapshot.revenueUsdDelta:+0.0;-0.0;0}% vs yesterday · Money (USD)";
                RevenueFcVsYesterdayText = snapshot.yesterdayRevenueFcValue <= 0m && snapshot.todayRevenueFcValue <= 0m
                    ? "No FC revenue yesterday to compare"
                    : $"{snapshot.revenueFcDelta:+0.0;-0.0;0}% vs yesterday · Money (FC)";
                ExpensesVsYesterdayText = snapshot.yesterdayExpensesUsdValue <= 0m && snapshot.todayExpensesUsdValue <= 0m
                    ? "No expenses yesterday to compare"
                    : $"{snapshot.expensesUsdDelta:+0.0;-0.0;0}% vs yesterday · all in USD";

                OrdersCompletedToday = snapshot.ordersCompletedToday;
                CompletedVsYesterdayText = snapshot.ordersCompletedYesterday == 0 && snapshot.ordersCompletedToday == 0
                    ? "No completions yesterday"
                    : $"{snapshot.completedDelta:+0.0;-0.0;0}% vs yesterday";

                ActiveOrdersCount = snapshot.activeOrdersCount;
                InKitchenNowText = $"{snapshot.inKitchenCount} in kitchen now";
                ClockedInStaff = $"{snapshot.clockedInCount}/{Math.Max(snapshot.totalActiveEmployees, 0)}";
                NotYetInText = $"{Math.Max(snapshot.totalActiveEmployees - snapshot.clockedInCount, 0)} not yet in";
                OccupiedTablesText = $"{snapshot.occupiedTables}/{snapshot.totalTables} occupied";
                OccupiedTablesPercent = snapshot.totalTables > 0
                    ? 100.0 * snapshot.occupiedTables / snapshot.totalTables
                    : 0;

                BuildChart(snapshot.revenueByDay);
                BuildSparkline(snapshot.revenueLast7);
                BuildHourlyChart(snapshot.hourlySales);

                CriticalInventoryAlerts.Clear();
                foreach (var a in snapshot.criticalAlerts.Take(8))
                    CriticalInventoryAlerts.Add(a);
                ReorderInventoryAlerts.Clear();
                foreach (var a in snapshot.reorderAlerts.Take(8))
                    ReorderInventoryAlerts.Add(a);

                TopSellingDishes.Clear();
                foreach (var d in snapshot.topDishVms)
                    TopSellingDishes.Add(d);

                StaffPresence.Clear();
                foreach (var s in snapshot.staffPresence)
                    StaffPresence.Add(s);

                RecentActivities.Clear();
                foreach (var item in snapshot.activities)
                    RecentActivities.Add(item);

                _drilldownCache[DashboardDrilldownType.TodaySales] = snapshot.salesItems;
                _drilldownCache[DashboardDrilldownType.ActiveOrders] = snapshot.activeOrderItems;
                _drilldownCache[DashboardDrilldownType.LowStockAlerts] = snapshot.lowStockItems;
                _drilldownCache[DashboardDrilldownType.ClockedInStaff] = snapshot.attendanceItems;
                _drilldownCache[DashboardDrilldownType.WeeklyRevenue] = snapshot.revenueItems;
                _drilldownCache[DashboardDrilldownType.RecentActivity] = snapshot.activityItems;

                OnPropertyChanged(nameof(TodayRevenueUsd));
                OnPropertyChanged(nameof(TodayRevenueFc));
                OnPropertyChanged(nameof(TodayExpensesUsd));
                OnPropertyChanged(nameof(RevenueUsdVsYesterdayText));
                OnPropertyChanged(nameof(RevenueFcVsYesterdayText));
                OnPropertyChanged(nameof(ExpensesVsYesterdayText));
                OnPropertyChanged(nameof(SparklinePoints));
                OnPropertyChanged(nameof(OrdersCompletedToday));
                OnPropertyChanged(nameof(CompletedVsYesterdayText));
                OnPropertyChanged(nameof(ActiveOrdersCount));
                OnPropertyChanged(nameof(ClockedInStaff));
                OnPropertyChanged(nameof(InKitchenNowText));
                OnPropertyChanged(nameof(NotYetInText));
                OnPropertyChanged(nameof(OccupiedTablesText));
                OnPropertyChanged(nameof(OccupiedTablesPercent));
                OnPropertyChanged(nameof(DatabaseConnected));
                OnPropertyChanged(nameof(DatabaseStatusText));
                OnPropertyChanged(nameof(ShowSalaryPayrollWarning));
                OnPropertyChanged(nameof(SalaryPayrollWarningText));
                OnPropertyChanged(nameof(SalaryPayrollDaysPastDue));
                OnPropertyChanged(nameof(ChartAreaPoints));
                OnPropertyChanged(nameof(ChartLinePoints));
                OnPropertyChanged(nameof(WeeklyChartMaxLabel));
                OnPropertyChanged(nameof(HourlyChartAreaPoints));
                OnPropertyChanged(nameof(HourlyChartLinePoints));
                OnPropertyChanged(nameof(HourlyChartMaxLabel));
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DatabaseConnected = false;
                DatabaseStatusText = "Local database · Error";
                OnPropertyChanged(nameof(DatabaseConnected));
                OnPropertyChanged(nameof(DatabaseStatusText));
                RecentActivities.Clear();
                RecentActivities.Add(new ActivityItem
                {
                    Time = DateTime.Now.ToString("HH:mm"),
                    Title = "Dashboard load error",
                    Description = ex.Message
                });
            });
        }
    }

    private void BuildChart(IReadOnlyList<decimal> values)
    {
        var pointsX = new[] { 0, 145, 290, 435, 580 };
        var max = Math.Max(values.Count > 0 ? values.Max() : 0m, 1m);
        var step = max <= 200m ? 50m : 250m;
        WeeklyChartMaxLabel = $"${Math.Ceiling(max / step) * step:0}";
        var linePoints = new List<string>();

        for (var i = 0; i < pointsX.Length; i++)
        {
            var value = i < values.Count ? values[i] : 0m;
            var y = 160d - ((double)(value / max) * 157d);
            linePoints.Add($"{pointsX[i]},{Math.Clamp(y, 3d, 160d):0.##}");
        }

        ChartLinePoints = string.Join(" ", linePoints);
        ChartAreaPoints = $"{ChartLinePoints} 580,160 0,160";
    }

    private void BuildSparkline(IReadOnlyList<decimal> values)
    {
        const double w = 120;
        const double h = 28;
        var max = Math.Max(values.Count > 0 ? values.Max() : 0m, 1m);
        var pts = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count <= 1 ? 0 : i * (w / (values.Count - 1));
            var val = values[i];
            var y = h - (double)(val / max) * (h - 2);
            pts.Add($"{x:0.##},{Math.Clamp(y, 1d, h):0.##}");
        }

        SparklinePoints = pts.Count > 0 ? string.Join(" ", pts) : $"0,{h} {w},{h}";
    }

    private void BuildHourlyChart(decimal[] hourly24)
    {
        const double w = 640;
        const double h = 140;
        var max = Math.Max(hourly24.DefaultIfEmpty().Max(), 1m);
        HourlyChartMaxLabel = $"${max:N0}";
        var linePoints = new List<string>();
        for (var hour = 0; hour < 24; hour++)
        {
            var x = hour * (w / 23d);
            var val = hourly24[hour];
            var y = h - (double)(val / max) * (h - 4);
            linePoints.Add($"{x:0.##},{Math.Clamp(y, 2d, h):0.##}");
        }

        HourlyChartLinePoints = string.Join(" ", linePoints);
        HourlyChartAreaPoints = $"{HourlyChartLinePoints} {w},{h} 0,{h}";
    }
}
