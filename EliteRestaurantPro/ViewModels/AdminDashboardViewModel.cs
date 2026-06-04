using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.ViewModels;

/// <summary>One row in the dashboard inventory list (color from <see cref="QuantityBand"/>).</summary>
public sealed class DashboardInventoryRowItem
{
    public string Name { get; init; } = string.Empty;
    public string QuantityLine { get; init; } = string.Empty;
    /// <summary>Matches <see cref="InventoryItem.QuantityStatus"/>: Out, Critical, Low, Healthy.</summary>
    public string QuantityBand { get; init; } = string.Empty;
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
    public string OnDutyLabel { get; init; } = string.Empty;
}

internal sealed class DashboardLoadedSnapshot
{
    public bool DbOk { get; init; }
    public decimal TodayRevenueUsdValue { get; init; }
    public decimal YesterdayRevenueUsdValue { get; init; }
    public decimal TodayRevenueFcValue { get; init; }
    public decimal YesterdayRevenueFcValue { get; init; }
    public decimal TodayExpensesUsdValue { get; init; }
    public decimal YesterdayExpensesUsdValue { get; init; }
    public decimal RevenueUsdDelta { get; init; }
    public decimal RevenueFcDelta { get; init; }
    public decimal ExpensesUsdDelta { get; init; }
    public int OrdersCompletedToday { get; init; }
    public int OrdersCompletedYesterday { get; init; }
    public decimal CompletedDelta { get; init; }
    public int ActiveOrdersCount { get; init; }
    public int InKitchenCount { get; init; }
    public int TotalActiveEmployees { get; init; }
    public int ClockedInCount { get; init; }
    public int OccupiedTables { get; init; }
    public int TotalTables { get; init; }
    public IReadOnlyList<decimal> RevenueByDay { get; init; } = [];
    public IReadOnlyList<decimal> RevenueLast7 { get; init; } = [];
    public decimal[] HourlySales { get; init; } = [];
    public List<ActivityItem> Activities { get; init; } = [];
    public List<DashboardDrilldownItem> SalesItems { get; init; } = [];
    public List<DashboardDrilldownItem> ActiveOrderItems { get; init; } = [];
    public List<DashboardDrilldownItem> LowStockItems { get; init; } = [];
    public List<DashboardDrilldownItem> AttendanceItems { get; init; } = [];
    public List<DashboardDrilldownItem> RevenueItems { get; init; } = [];
    public List<DashboardDrilldownItem> ActivityItems { get; init; } = [];
    public List<DashboardInventoryRowItem> InventoryRows { get; init; } = [];
    public List<DashboardStaffPresenceItem> StaffPresence { get; init; } = [];
    public List<DashboardTopDishItem> TopDishVms { get; init; } = [];
    public bool SalaryWarningShow { get; init; }
    public string SalaryWarningMessage { get; init; } = string.Empty;
    public int SalaryDaysPast { get; init; }
}

public class AdminDashboardViewModel : AdminBaseViewModel
{
    private readonly Dictionary<DashboardDrilldownType, List<DashboardDrilldownItem>> _drilldownCache = [];
    private bool _isNavigatingToShortcut;
    private DispatcherTimer? _clockTimer;
    private DashboardLoadedSnapshot? _lastSnapshot;

    public override string ActivePage => "Dashboard";

    public string WelcomeTitle
    {
        get
        {
            var name = !string.IsNullOrWhiteSpace(AppSession.AdminLoginDisplayName)
                ? AppSession.AdminLoginDisplayName!
                : Loc.Admin("dashOwnerFallback", "Owner");
            return Loc.Admin("dashWelcome", "Welcome, {{name}}", new Dictionary<string, string> { ["name"] = name });
        }
    }

    public string DashSubtitle => Loc.Admin("dashSubtitle", "Operations command center — today's performance, inventory risk, and team status.");
    public string SalaryPayrollOverdueTitle => Loc.Admin("dashSalaryOverdueTitle", "Salary payroll overdue");
    public string DaysPastMonthEndLabel => Loc.Admin("dashDaysPastMonthEnd", "Days past month end:");
    public string KpiTodayUsdTitle => Loc.Admin("kpiTodayUsd", "Today's revenue (USD)");
    public string KpiTodayUsdSub => Loc.Admin("kpiTodayUsdSub", "Money — entries in USD only");
    public string KpiTodayFcTitle => Loc.Admin("kpiTodayFc", "Today's revenue (FC)");
    public string KpiTodayFcSub => Loc.Admin("kpiTodayFcSub", "Money — entries in FC only (not converted from USD)");
    public string KpiTodayExpTitle => Loc.Admin("kpiTodayExp", "Today's expenses");
    public string KpiTodayExpSub => Loc.Admin("kpiTodayExpSub", "All expenses in USD (FC rows use stored USD equivalent)");
    public string KpiTablesTitle => Loc.Admin("kpiTables", "Tables occupied");
    public string KpiActiveOrdersTitle => Loc.Admin("kpiActiveOrders", "Active orders");
    public string KpiCompletedTodayTitle => Loc.Admin("kpiCompletedToday", "Orders completed today");
    public string KpiCompletedTodaySub => Loc.Admin("kpiCompletedTodaySub", "From orders (not Money ledger)");
    public string DashSparklineCap => Loc.Admin("dashSparklineCap", "Sparkline: 7-day USD revenue (Money)");
    public string DashInventoryTitle => Loc.Admin("dashInventoryAlerts", "Inventory alerts");
    public string DashInventoryBtn => Loc.Admin("dashInventoryBtn", "Inventory");
    public string DashSalesRhythmTitle => Loc.Admin("dashSalesRhythm", "Today's sales rhythm");
    public string DashSalesCaption => Loc.Admin("dashSalesCaption", "Revenue by hour (completed & open orders created today).");
    public string DashTopDishesTitle => Loc.Admin("dashTopDishes", "Top dishes today");
    public string DashTeamRosterTitle => Loc.Admin("dashTeamRoster", "Team on roster");
    public string DashAttendanceBtn => Loc.Admin("dashAttendance", "Attendance");
    public string DashRecentActivityTitle => Loc.Admin("dashRecentActivity", "Recent activity");
    public string DashWeeklyRevenueTitle => Loc.Admin("dashWeeklyRevenue", "Weekly revenue");
    public string DashThisWeekLabel => Loc.Admin("dashThisWeek", "This week (Mon–Fri)");
    public string DashRecentHint => Loc.Admin("dashRecentHint", "Last {{count}} events · tap a card to open orders, team, or inventory.",
        new Dictionary<string, string> { ["count"] = DashboardDrilldownViewModel.RecentActivityMaxItems.ToString(CultureInfo.InvariantCulture) });

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
    public string ActiveOrdersSubText { get; private set; } = string.Empty;
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

    public string HourlyChartAreaPoints { get; private set; } = "0,140 640,140 640,140 0,140";
    public string HourlyChartLinePoints { get; private set; } = "0,140 640,140";
    public string HourlyChartMaxLabel { get; private set; } = "$0";

    public ObservableCollection<DashboardInventoryRowItem> InventoryDashboardRows { get; } = [];
    public ObservableCollection<DashboardTopDishItem> TopSellingDishes { get; } = [];
    public ObservableCollection<DashboardStaffPresenceItem> StaffPresence { get; } = [];
    public ObservableCollection<ActivityFeedItem> RecentActivities { get; } = [];

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
        OpenActivityItemCommand = new RelayCommand(activity => OpenFromActivity(activity as ActivityFeedItem));

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
    {
        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;
        var now = RestaurantTimeZone.UtcToRestaurant(DateTime.UtcNow, tz);
        var culture = Loc.Language == "fr" ? "fr-FR" : "en-US";
        LiveClockText = now.ToString("dddd, MMMM dd, yyyy  ·  HH:mm", CultureInfo.GetCultureInfo(culture));
    }

    protected override void RefreshLocalizedStrings()
    {
        UpdateLiveClock();
        Notify(
            nameof(WelcomeTitle),
            nameof(DashSubtitle),
            nameof(SalaryPayrollOverdueTitle),
            nameof(DaysPastMonthEndLabel),
            nameof(KpiTodayUsdTitle),
            nameof(KpiTodayUsdSub),
            nameof(KpiTodayFcTitle),
            nameof(KpiTodayFcSub),
            nameof(KpiTodayExpTitle),
            nameof(KpiTodayExpSub),
            nameof(KpiTablesTitle),
            nameof(KpiActiveOrdersTitle),
            nameof(KpiCompletedTodayTitle),
            nameof(KpiCompletedTodaySub),
            nameof(DashSparklineCap),
            nameof(DashInventoryTitle),
            nameof(DashInventoryBtn),
            nameof(DashSalesRhythmTitle),
            nameof(DashSalesCaption),
            nameof(DashTopDishesTitle),
            nameof(DashTeamRosterTitle),
            nameof(DashAttendanceBtn),
            nameof(DashRecentActivityTitle),
            nameof(DashRecentHint),
            nameof(DashWeeklyRevenueTitle),
            nameof(DashThisWeekLabel),
            nameof(LiveClockText));

        if (_lastSnapshot is not null)
            ApplySnapshotToUi(_lastSnapshot);
    }

    private void OpenDrilldown(DashboardDrilldownType type)
    {
        if (_isNavigatingToShortcut)
            return;

        var title = type switch
        {
            DashboardDrilldownType.TodaySales => "Today's Sales",
            DashboardDrilldownType.ActiveOrders => "Active Orders",
            DashboardDrilldownType.LowStockAlerts => "Inventory",
            DashboardDrilldownType.ClockedInStaff => "Clocked-In Staff",
            DashboardDrilldownType.WeeklyRevenue => "Weekly Revenue",
            _ => "Recent Activity"
        };

        var subtitle = type switch
        {
            DashboardDrilldownType.TodaySales => "All non-cancelled orders created today.",
            DashboardDrilldownType.ActiveOrders => "Orders currently in service flow.",
            DashboardDrilldownType.LowStockAlerts => "On hand (lowest first).",
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

    private void OpenFromActivity(ActivityFeedItem? activity)
    {
        OpenDrilldown(DashboardDrilldownType.RecentActivity);
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            var snapshot = await Task.Run(async () =>
            {
                var dbOk = true;
                var today = DateTime.Today;
                var tz = RestaurantTimeZone.NormalizeId(SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId);
                var data = new AdminDataApiClient();
                List<OrderRecord> allOrders;
                List<Table> dbTablesSnapshot;
                List<Product> products;
                List<OrderItem> orderItems;
                List<InventoryItem> inventorySnapshot;
                List<Employee> employeesSnapshot;
                List<EmployeeAttendance> attendancesSnapshot;
                List<MoneyTransaction> allMoneyRows;
                List<PayrollPaymentRecord> payrollSnapshot;
                try
                {
                    var ordersTask = data.GetOrdersAsync();
                    var tablesTask = data.GetTablesAsync();
                    var productsTask = data.GetProductsAsync();
                    var inventoryTask = data.GetInventoryItemsAsync();
                    var employeesTask = data.GetEmployeesAsync();
                    var attendanceTask = data.GetAttendanceAsync();
                    var moneyTask = data.GetMoneyTransactionsAsync();
                    var payrollTask = data.GetPayrollAsync();
                    await Task.WhenAll(
                        ordersTask,
                        tablesTask,
                        productsTask,
                        inventoryTask,
                        employeesTask,
                        attendanceTask,
                        moneyTask,
                        payrollTask).ConfigureAwait(false);
                    allOrders = (await ordersTask.ConfigureAwait(false)).ToList();
                    dbTablesSnapshot = (await tablesTask.ConfigureAwait(false)).ToList();
                    products = (await productsTask.ConfigureAwait(false)).ToList();
                    inventorySnapshot = (await inventoryTask.ConfigureAwait(false)).ToList();
                    employeesSnapshot = (await employeesTask.ConfigureAwait(false)).ToList();
                    attendancesSnapshot = (await attendanceTask.ConfigureAwait(false)).ToList();
                    allMoneyRows = (await moneyTask.ConfigureAwait(false)).ToList();
                    payrollSnapshot = (await payrollTask.ConfigureAwait(false)).ToList();
                    orderItems = allOrders.SelectMany(o => o.Items).ToList();
                }
                catch
                {
                    dbOk = false;
                    allOrders = [];
                    dbTablesSnapshot = [];
                    products = [];
                    orderItems = [];
                    inventorySnapshot = [];
                    employeesSnapshot = [];
                    attendancesSnapshot = [];
                    allMoneyRows = [];
                    payrollSnapshot = [];
                }

                var (attendanceTodayStartUtc, attendanceTodayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(today);
                var tomorrow = today.AddDays(1);
                var yesterday = today.AddDays(-1);
                var activeStatuses = new[]
                {
                    "Waiting", "In Kitchen", "Ready", OrderWorkflow.Served, OrderWorkflow.PendingCashier,
                    OrderWorkflow.PendingApproval
                };

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

                var moneyWindowStart = today.AddDays(-6);
                var txMoneyWindow = allMoneyRows
                    .Where(t => t.Date >= moneyWindowStart && t.Date < tomorrow)
                    .ToList();
                var todaysOrders = allOrders
                    .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();
                var yesterdaysOrders = allOrders
                    .Where(o => o.CreatedAt >= yesterday && o.CreatedAt < today && !string.Equals(o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
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
                var totalActiveEmployees = employeesSnapshot.Count(e => e.EmploymentStatus == "Active");
                var clockedInCount = attendancesSnapshot.Count(a =>
                    a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc && a.ClockInTime != null);
                var occupiedTables = dbTablesSnapshot.Count(t => t.Status == "Occupied");
                var totalTables = dbTablesSnapshot.Count;

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

                var inventoryRows = inventorySnapshot
                    .OrderBy(i => i.StockQuantity)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new DashboardInventoryRowItem
                    {
                        Name = item.Name,
                        QuantityLine = $"{item.StockQuantity:0.##} {item.Unit}",
                        QuantityBand = item.QuantityStatus
                    })
                    .ToList();

                var todayAttendance = attendancesSnapshot
                    .Where(a => a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc)
                    .ToDictionary(a => a.EmployeeId, a => a);

                var employeesById = employeesSnapshot.ToDictionary(e => e.Id);

                var staffPresence = employeesSnapshot
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.Name)
                    .ToList()
                    .Select(emp => new DashboardStaffPresenceItem
                    {
                        Name = emp.Name,
                        Role = emp.Role,
                        IsClockedIn = todayAttendance.TryGetValue(emp.Id, out var a) && a.ClockInTime != null,
                        OnDutyLabel = todayAttendance.TryGetValue(emp.Id, out var att) && att.ClockInTime != null
                            ? Loc.Admin("staffStatusActive", "Active")
                            : Loc.Admin("staffStatusOffDuty", "Off duty")
                    })
                    .ToList();

                List<ActivityItem> activities;
                try
                {
                    activities = DashboardDrilldownViewModel.BuildRecentActivities(
                        allOrders,
                        attendancesSnapshot,
                        employeesById,
                        inventorySnapshot);
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
                        Detail = $"Status: {order.Status} · {RestaurantTimeZone.FormatUtc(order.CreatedAt, tz, "HH:mm")}",
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

                var lowStockItems = inventorySnapshot
                    .OrderBy(i => i.StockQuantity)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new DashboardDrilldownItem
                    {
                        Title = item.Name,
                        Subtitle = string.Empty,
                        Detail = $"{item.StockQuantity:0.##} {item.Unit}",
                        Meta = string.Empty,
                        AccentColor = DrilldownAccentForQuantityBand(item.QuantityStatus)
                    })
                    .ToList();

                var attendanceItems = attendancesSnapshot
                    .Where(a =>
                        a.WorkDate >= attendanceTodayStartUtc && a.WorkDate < attendanceTodayEndExclusiveUtc && a.ClockInTime != null)
                    .OrderBy(a => a.ClockInTime)
                    .Select(row =>
                    {
                        var emp = employeesById.TryGetValue(row.EmployeeId, out var e) ? e : null;
                        return new DashboardDrilldownItem
                        {
                            Title = emp != null ? emp.Name : "Unknown Employee",
                            Subtitle = emp != null ? emp.UniqueId : string.Empty,
                            Detail = $"Clock In: {row.ClockInTime:HH:mm} · Clock Out: {(row.ClockOutTime.HasValue ? row.ClockOutTime.Value.ToString("HH:mm") : "Not clocked out")}",
                            Meta = string.IsNullOrWhiteSpace(row.ClockInStatus) ? "Status: Pending" : $"Status: {row.ClockInStatus}",
                            AccentColor = row.ClockInStatus == "Late" ? "#F44336" : "#2196F3"
                        };
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

                var salaryOverdue = FinancialTransactionService.GetSalaryOverdueState(
                    DateTime.Now,
                    employeesSnapshot,
                    payrollSnapshot,
                    allMoneyRows);

                return new DashboardLoadedSnapshot
                {
                    DbOk = dbOk,
                    TodayRevenueUsdValue = todayRevenueUsdValue,
                    YesterdayRevenueUsdValue = yesterdayRevenueUsdValue,
                    TodayRevenueFcValue = todayRevenueFcValue,
                    YesterdayRevenueFcValue = yesterdayRevenueFcValue,
                    TodayExpensesUsdValue = todayExpensesUsdValue,
                    YesterdayExpensesUsdValue = yesterdayExpensesUsdValue,
                    RevenueUsdDelta = revenueUsdDelta,
                    RevenueFcDelta = revenueFcDelta,
                    ExpensesUsdDelta = expensesUsdDelta,
                    OrdersCompletedToday = ordersCompletedToday,
                    OrdersCompletedYesterday = ordersCompletedYesterday,
                    CompletedDelta = completedDelta,
                    ActiveOrdersCount = activeOrdersCount,
                    InKitchenCount = inKitchenCount,
                    TotalActiveEmployees = totalActiveEmployees,
                    ClockedInCount = clockedInCount,
                    OccupiedTables = occupiedTables,
                    TotalTables = totalTables,
                    RevenueByDay = revenueByDay,
                    RevenueLast7 = revenueLast7,
                    HourlySales = hourlySales,
                    Activities = activities,
                    SalesItems = salesItems,
                    ActiveOrderItems = activeOrderItems,
                    LowStockItems = lowStockItems,
                    AttendanceItems = attendanceItems,
                    RevenueItems = revenueItems,
                    ActivityItems = activityItems,
                    InventoryRows = inventoryRows,
                    StaffPresence = staffPresence,
                    TopDishVms = topDishVms,
                    SalaryWarningShow = salaryOverdue.ShowWarning,
                    SalaryWarningMessage = salaryOverdue.Message,
                    SalaryDaysPast = salaryOverdue.DaysPastPayDay
                };
            });

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _lastSnapshot = snapshot;
                ApplySnapshotToUi(snapshot);
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                DatabaseConnected = false;
                DatabaseStatusText = Loc.Admin("dashApiError", "Cloud API · Error");
                BuildHourlyChart(new decimal[24]);
                OnPropertyChanged(nameof(DatabaseConnected));
                OnPropertyChanged(nameof(DatabaseStatusText));
                OnPropertyChanged(nameof(HourlyChartAreaPoints));
                OnPropertyChanged(nameof(HourlyChartLinePoints));
                OnPropertyChanged(nameof(HourlyChartMaxLabel));
                RecentActivities.Clear();
                RecentActivities.Add(ActivityFeedItem.From(new ActivityItem
                {
                    Time = DateTime.Now.ToString("HH:mm"),
                    Title = Loc.Admin("dashLoadError", "Dashboard load error"),
                    Description = ex.Message
                }));
            });
        }
    }

    private void ApplySnapshotToUi(DashboardLoadedSnapshot snapshot)
    {
        DatabaseConnected = snapshot.DbOk;
        DatabaseStatusText = snapshot.DbOk
            ? Loc.Admin("dashApiConnected", "Cloud API · Connected")
            : Loc.Admin("dashApiOffline", "Cloud API · Offline");

        ShowSalaryPayrollWarning = snapshot.SalaryWarningShow;
        SalaryPayrollDaysPastDue = snapshot.SalaryDaysPast;
        SalaryPayrollWarningText = snapshot.SalaryWarningShow
            ? FormatSalaryPayrollWarningText(snapshot.SalaryDaysPast)
            : string.Empty;

        TodayRevenueUsd = CurrencyHelper.FormatAmount(snapshot.TodayRevenueUsdValue, CurrencyHelper.Usd);
        TodayRevenueFc = CurrencyHelper.FormatAmount(snapshot.TodayRevenueFcValue, CurrencyHelper.CongoleseFranc);
        TodayExpensesUsd = CurrencyHelper.FormatAmount(snapshot.TodayExpensesUsdValue, CurrencyHelper.Usd);

        RevenueUsdVsYesterdayText = snapshot.YesterdayRevenueUsdValue <= 0m && snapshot.TodayRevenueUsdValue <= 0m
            ? Loc.Admin("kpiNoCompareUsd", "No USD revenue yesterday to compare. Money — entries in USD only.")
            : Loc.Admin("kpiDeltaUsd", "{{delta}}% vs yesterday · Money (USD).",
                new Dictionary<string, string> { ["delta"] = snapshot.RevenueUsdDelta.ToString("+0.0;-0.0;0", CultureInfo.InvariantCulture) });
        RevenueFcVsYesterdayText = snapshot.YesterdayRevenueFcValue <= 0m && snapshot.TodayRevenueFcValue <= 0m
            ? Loc.Admin("kpiNoCompareFc", "No FC revenue yesterday to compare. Money — entries in FC only (not converted from USD).")
            : Loc.Admin("kpiDeltaFc", "{{delta}}% vs yesterday · Money (FC).",
                new Dictionary<string, string> { ["delta"] = snapshot.RevenueFcDelta.ToString("+0.0;-0.0;0", CultureInfo.InvariantCulture) });
        ExpensesVsYesterdayText = snapshot.YesterdayExpensesUsdValue <= 0m && snapshot.TodayExpensesUsdValue <= 0m
            ? Loc.Admin("kpiNoCompareExp", "No expenses yesterday to compare. All expenses in USD (FC rows use stored USD equivalent).")
            : Loc.Admin("kpiDeltaExp", "{{delta}}% vs yesterday · all in USD.",
                new Dictionary<string, string> { ["delta"] = snapshot.ExpensesUsdDelta.ToString("+0.0;-0.0;0", CultureInfo.InvariantCulture) });

        OrdersCompletedToday = snapshot.OrdersCompletedToday;
        CompletedVsYesterdayText = snapshot.OrdersCompletedYesterday == 0 && snapshot.OrdersCompletedToday == 0
            ? Loc.Admin("kpiNoCompareCompleted", "No completions yesterday. From orders (not Money ledger).")
            : Loc.Admin("kpiDeltaCompleted", "{{delta}}% vs yesterday.",
                new Dictionary<string, string> { ["delta"] = snapshot.CompletedDelta.ToString("+0.0;-0.0;0", CultureInfo.InvariantCulture) });

        ActiveOrdersCount = snapshot.ActiveOrdersCount;
        InKitchenNowText = Loc.Admin("kpiActiveOrdersSub", "{{count}} in kitchen now",
            new Dictionary<string, string> { ["count"] = snapshot.InKitchenCount.ToString(CultureInfo.InvariantCulture) });
        ClockedInStaff = $"{snapshot.ClockedInCount}/{Math.Max(snapshot.TotalActiveEmployees, 0)}";
        NotYetInText = Loc.Admin("dashNotYetIn", "{{count}} not yet in",
            new Dictionary<string, string> { ["count"] = Math.Max(snapshot.TotalActiveEmployees - snapshot.ClockedInCount, 0).ToString(CultureInfo.InvariantCulture) });
        OccupiedTablesText = Loc.Admin("kpiTablesPrimary", "{{occupied}}/{{total}} occupied",
            new Dictionary<string, string>
            {
                ["occupied"] = snapshot.OccupiedTables.ToString(CultureInfo.InvariantCulture),
                ["total"] = snapshot.TotalTables.ToString(CultureInfo.InvariantCulture)
            });
        ActiveOrdersSubText = Loc.Admin("kpiTablesSub", "{{count}} active orders",
            new Dictionary<string, string> { ["count"] = snapshot.ActiveOrdersCount.ToString(CultureInfo.InvariantCulture) });
        OccupiedTablesPercent = snapshot.TotalTables > 0
            ? 100.0 * snapshot.OccupiedTables / snapshot.TotalTables
            : 0;

        BuildChart(snapshot.RevenueByDay);
        BuildSparkline(snapshot.RevenueLast7);
        BuildHourlyChart(snapshot.HourlySales);

        InventoryDashboardRows.Clear();
        foreach (var row in snapshot.InventoryRows)
            InventoryDashboardRows.Add(row);

        TopSellingDishes.Clear();
        foreach (var d in snapshot.TopDishVms)
            TopSellingDishes.Add(d);

        StaffPresence.Clear();
        foreach (var s in snapshot.StaffPresence.Select(RebuildStaffPresenceRow))
            StaffPresence.Add(s);

        RecentActivities.Clear();
        foreach (var item in snapshot.Activities.Select(ActivityFeedItem.From))
            RecentActivities.Add(item);

        _drilldownCache[DashboardDrilldownType.TodaySales] = snapshot.SalesItems;
        _drilldownCache[DashboardDrilldownType.ActiveOrders] = snapshot.ActiveOrderItems;
        _drilldownCache[DashboardDrilldownType.LowStockAlerts] = snapshot.LowStockItems;
        _drilldownCache[DashboardDrilldownType.ClockedInStaff] = snapshot.AttendanceItems;
        _drilldownCache[DashboardDrilldownType.WeeklyRevenue] = snapshot.RevenueItems;
        _drilldownCache[DashboardDrilldownType.RecentActivity] = snapshot.ActivityItems;

        Notify(
            nameof(TodayRevenueUsd),
            nameof(TodayRevenueFc),
            nameof(TodayExpensesUsd),
            nameof(RevenueUsdVsYesterdayText),
            nameof(RevenueFcVsYesterdayText),
            nameof(ExpensesVsYesterdayText),
            nameof(SparklinePoints),
            nameof(OrdersCompletedToday),
            nameof(CompletedVsYesterdayText),
            nameof(ActiveOrdersCount),
            nameof(ActiveOrdersSubText),
            nameof(ClockedInStaff),
            nameof(InKitchenNowText),
            nameof(NotYetInText),
            nameof(OccupiedTablesText),
            nameof(OccupiedTablesPercent),
            nameof(DatabaseConnected),
            nameof(DatabaseStatusText),
            nameof(ShowSalaryPayrollWarning),
            nameof(SalaryPayrollWarningText),
            nameof(SalaryPayrollDaysPastDue),
            nameof(ChartAreaPoints),
            nameof(ChartLinePoints),
            nameof(WeeklyChartMaxLabel),
            nameof(HourlyChartAreaPoints),
            nameof(HourlyChartLinePoints),
            nameof(HourlyChartMaxLabel));
    }

    private static DashboardStaffPresenceItem RebuildStaffPresenceRow(DashboardStaffPresenceItem row) => new()
    {
        Name = row.Name,
        Role = AdminTextLocalizer.TranslateRole(row.Role),
        IsClockedIn = row.IsClockedIn,
        OnDutyLabel = row.IsClockedIn
            ? Loc.Admin("staffStatusActive", "Active")
            : Loc.Admin("staffStatusOffDuty", "Off duty")
    };

    private static string FormatSalaryPayrollWarningText(int daysPast)
    {
        var today = DateTime.Today;
        var firstThisMonth = new DateTime(today.Year, today.Month, 1);
        var lastDayPrevMonth = firstThisMonth.AddDays(-1);
        var culture = Loc.Language == "fr" ? "fr-FR" : "en-US";
        var monthYear = new DateTime(lastDayPrevMonth.Year, lastDayPrevMonth.Month, 1)
            .ToString("MMMM yyyy", CultureInfo.GetCultureInfo(culture));
        return Loc.Admin(
            "dashSalaryOverdueBody",
            "Payroll for {{monthYear}} is not fully posted. Use Salary to record payments ({{days}} day(s) past month end).",
            new Dictionary<string, string>
            {
                ["monthYear"] = monthYear,
                ["days"] = daysPast.ToString(CultureInfo.InvariantCulture)
            });
    }

    private static string ChartPoint(double x, double y) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", x, y);

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
            linePoints.Add(ChartPoint(pointsX[i], Math.Clamp(y, 3d, 160d)));
        }

        ChartLinePoints = string.Join(" ", linePoints);
        ChartAreaPoints = $"{ChartLinePoints} {ChartPoint(580, 160)} {ChartPoint(0, 160)}";
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
            pts.Add(ChartPoint(x, Math.Clamp(y, 1d, h)));
        }

        SparklinePoints = pts.Count > 0 ? string.Join(" ", pts) : $"{ChartPoint(0, h)} {ChartPoint(w, h)}";
    }

    private void BuildHourlyChart(decimal[] hourly24)
    {
        const double w = 640;
        const double h = 140;
        var total = hourly24.Sum();
        var max = hourly24.Length > 0 ? hourly24.Max() : 0m;
        var hasData = total > 0.005m;
        var scaleMax = hasData ? Math.Max(max, 0.01m) : 1m;
        HourlyChartMaxLabel = hasData
            ? $"${max.ToString("N0", CultureInfo.InvariantCulture)}"
            : "$0";
        var linePoints = new List<string>();
        for (var hour = 0; hour < 24; hour++)
        {
            var x = hour * (w / 23d);
            var val = hour < hourly24.Length ? hourly24[hour] : 0m;
            var y = hasData
                ? h - (double)(val / scaleMax) * (h - 4)
                : h;
            linePoints.Add(ChartPoint(x, Math.Clamp(y, 2d, h)));
        }

        HourlyChartLinePoints = string.Join(" ", linePoints);
        HourlyChartAreaPoints = $"{HourlyChartLinePoints} {ChartPoint(w, h)} {ChartPoint(0, h)}";
    }

    private static string DrilldownAccentForQuantityBand(string band) =>
        band switch
        {
            "Out" or "Critical" => "#F44336",
            "Low" => "#FF9800",
            _ => "#4CAF50"
        };
}
