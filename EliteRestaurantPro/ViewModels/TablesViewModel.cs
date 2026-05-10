using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public class TablesViewModel : AdminBaseViewModel
{
    private static readonly JsonSerializerOptions SyncJson = new(JsonSerializerDefaults.Web);
    private readonly AdminDataApiClient _data = new();

    private int? _editingTableId;
    private bool _isDialogOpen;
    private string _dialogTitle = "Add Table";
    private string _tableNumberText = string.Empty;
    private string _tableNameText = string.Empty;
    private string _capacityText = string.Empty;
    private string _selectedStatus = "Available";
    private int? _selectedServerId;
    private bool _isTableNumberEditable;
    private readonly List<Table> _allTables = [];
    private string _searchText = string.Empty;

    public override string ActivePage => "Tables";

    public bool ShowTableManagementChrome => !AppSession.IsStaffTablet;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            ApplyTablesFilter();
        }
    }

    public ObservableCollection<Table> Tables { get; } = new();
    public ObservableCollection<Employee> Servers { get; } = new();
    public ObservableCollection<string> Statuses { get; } =
        new(["Available", "Maintenance"]);

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetField(ref _isDialogOpen, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    public string TableNumberText
    {
        get => _tableNumberText;
        set => SetField(ref _tableNumberText, value);
    }

    public string CapacityText
    {
        get => _capacityText;
        set => SetField(ref _capacityText, value);
    }

    public string TableNameText
    {
        get => _tableNameText;
        set => SetField(ref _tableNameText, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetField(ref _selectedStatus, value);
    }

    public int? SelectedServerId
    {
        get => _selectedServerId;
        set => SetField(ref _selectedServerId, value);
    }

    public bool IsTableNumberEditable
    {
        get => _isTableNumberEditable;
        set => SetField(ref _isTableNumberEditable, value);
    }

    public ICommand OpenAddDialogCommand { get; }
    public ICommand EditTableCommand { get; }
    public ICommand DeleteTableCommand { get; }
    public ICommand SaveTableCommand { get; }
    public ICommand CancelDialogCommand { get; }

    public TablesViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        EditTableCommand = new RelayCommand(table => OpenEditDialog(table as Table));
        DeleteTableCommand = new RelayCommand(table => _ = DeleteTableAsync(table as Table));
        SaveTableCommand = new RelayCommand(_ => _ = SaveTableAsync());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        _ = LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        try
        {
            var employees = await _data.GetEmployeesAsync().ConfigureAwait(true);
            var tables = await _data.GetTablesAsync().ConfigureAwait(true);
            var servers = employees
                .Where(e => e.Role.Equals("server", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Name)
                .ToList();
            var orderedTables = tables.OrderBy(t => t.TableNumber).ToList();

            Servers.Clear();
            foreach (var s in servers)
                Servers.Add(s);

            _allTables.Clear();
            foreach (var t in orderedTables)
                _allTables.Add(t);

            ApplyTablesFilter();
            RefreshReadyPickupBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Could not load tables",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyTablesFilter()
    {
        var q = (_searchText ?? string.Empty).Trim();
        Tables.Clear();
        foreach (var table in _allTables)
        {
            if (q.Length == 0 || TableMatchesSearch(table, q))
                Tables.Add(table);
        }
    }

    private static bool TableMatchesSearch(Table table, string q)
    {
        bool Hit(string? s)
            => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var numText = table.TableNumber.ToString(CultureInfo.InvariantCulture);
        var capText = table.Capacity.ToString(CultureInfo.InvariantCulture);
        return Hit(table.Name)
               || Hit(table.UniqueId)
               || Hit(table.Status)
               || numText.Contains(q, StringComparison.OrdinalIgnoreCase)
               || capText.Contains(q, StringComparison.OrdinalIgnoreCase)
               || Hit(table.AssignedServer?.Name);
    }

    private void OpenAddDialog()
    {
        if (AppSession.IsStaffTablet) return;

        _editingTableId = null;
        DialogTitle = "Add Table";
        TableNumberText = "Auto-assigned";
        TableNameText = string.Empty;
        CapacityText = string.Empty;
        SelectedStatus = Statuses.First();
        SelectedServerId = Servers.FirstOrDefault()?.Id;
        IsTableNumberEditable = false;
        IsDialogOpen = true;
    }

    private void OpenEditDialog(Table? table)
    {
        if (table is null) return;
        if (AppSession.IsStaffTablet) return;

        _editingTableId = table.Id;
        DialogTitle = "Edit Table";
        TableNumberText = table.TableNumber.ToString(CultureInfo.InvariantCulture);
        TableNameText = table.Name;
        CapacityText = table.Capacity.ToString(CultureInfo.InvariantCulture);
        SelectedStatus = table.Status == "Occupied" ? "Available" : table.Status;
        SelectedServerId = table.AssignedServerId;
        IsTableNumberEditable = true;
        IsDialogOpen = true;
    }

    private static bool HasBlockingOrdersForTable(IReadOnlyList<OrderRecord> orders, int tableId) =>
        orders.Any(o =>
            o.TableId == tableId &&
            (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
             o.Status == OrderWorkflow.Served ||
             o.Status == OrderWorkflow.PendingCashier ||
             o.Status == OrderWorkflow.PendingApproval));

    private async Task SaveTableAsync()
    {
        if (AppSession.IsStaffTablet) return;

        if (!int.TryParse(CapacityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity) ||
            string.IsNullOrWhiteSpace(SelectedStatus) ||
            string.IsNullOrWhiteSpace(TableNameText))
        {
            return;
        }

        var normalizedStatus = SelectedStatus.Trim();

        if (normalizedStatus == "Occupied")
        {
            MessageBox.Show(
                "You cannot manually set a table to Occupied. A table becomes Occupied only when it has an active order.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var orders = await _data.GetOrdersAsync().ConfigureAwait(true);

            if (normalizedStatus == "Maintenance" && _editingTableId is int maintenanceTableId)
            {
                if (HasBlockingOrdersForTable(orders, maintenanceTableId))
                {
                    MessageBox.Show(
                        "You cannot switch this table to Maintenance while active orders exist.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            if (normalizedStatus == "Available" && _editingTableId is int availableTableId)
            {
                if (HasBlockingOrdersForTable(orders, availableTableId))
                {
                    MessageBox.Show(
                        "This table has active orders and cannot be set to Available yet.",
                        "Validation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            if (_editingTableId is int tableId)
            {
                if (!int.TryParse(TableNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var editedTableNumber))
                    return;

                var baseRow = _allTables.FirstOrDefault(t => t.Id == tableId)
                    ?? throw new InvalidOperationException("Table not found in the current list. Refresh and try again.");

                var toPush = new Table
                {
                    Id = tableId,
                    UniqueId = baseRow.UniqueId,
                    TableNumber = editedTableNumber,
                    Name = TableNameText.Trim(),
                    Capacity = capacity,
                    Status = normalizedStatus,
                    AssignedServerId = SelectedServerId
                };

                try
                {
                    DesktopCloudPersistence.PushUpsertBlocking(toPush);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.GetBaseException().Message,
                        "Save table failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                var nextTableNumber = GetNextTableNumberFromNumbers(_allTables.Select(t => t.TableNumber));
                var confirmAdd = MessageBox.Show(
                    $"Add this table as ID {nextTableNumber}?",
                    "Confirm Add Table",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmAdd != MessageBoxResult.Yes)
                    return;

                var newTable = new Table
                {
                    UniqueId = UniqueIdGenerator.NewId("TBL"),
                    TableNumber = nextTableNumber,
                    Name = TableNameText.Trim(),
                    Capacity = capacity,
                    Status = normalizedStatus,
                    AssignedServerId = SelectedServerId
                };

                try
                {
                    DesktopCloudPersistence.PushUpsertBlocking(newTable);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.GetBaseException().Message,
                        "Add table failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            CloseDialog();
            await LoadTablesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Save table failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task DeleteTableAsync(Table? table)
    {
        if (table is null) return;
        if (AppSession.IsStaffTablet) return;

        var confirmDelete = MessageBox.Show(
            $"Delete table '{table.TableNumber} · {table.Name}'?",
            "Confirm Delete Table",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmDelete != MessageBoxResult.Yes)
            return;

        try
        {
            var orders = await _data.GetOrdersAsync().ConfigureAwait(true);

            if (HasBlockingOrdersForTable(orders, table.Id))
            {
                MessageBox.Show(
                    "This table has active orders and cannot be deleted.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var pastOrders = orders.Where(o =>
                o.TableId == table.Id &&
                (o.Status == "Completed" || o.Status == "Cancelled")).ToList();

            var tableCode = $"Table {table.TableNumber}";
            var tableName = string.IsNullOrWhiteSpace(table.Name) ? tableCode : table.Name;

            var ops = new List<CloudSyncOperation>();
            foreach (var order in pastOrders)
            {
                StripOrderNav(order);
                order.TableId = null;
                order.TableCode = tableCode;
                order.TableName = tableName;
                ops.Add(MakeUpsertOp(order));
            }

            ops.Add(MakeDeleteOp(CloneTableForSync(table)));

            try
            {
                DesktopCloudPersistence.PushBatchBlocking(ops);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.GetBaseException().Message,
                    "Delete table failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await LoadTablesAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.GetBaseException().Message,
                "Delete table failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static CloudSyncOperation MakeUpsertOp(OrderRecord order)
    {
        var json = JsonSerializer.Serialize(order, SyncJson);
        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            nameof(OrderRecord),
            "Upsert",
            json,
            DateTime.UtcNow);
    }

    private static CloudSyncOperation MakeDeleteOp(Table table)
    {
        var json = JsonSerializer.Serialize(table, SyncJson);
        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            nameof(Table),
            "Delete",
            json,
            DateTime.UtcNow);
    }

    private static Table CloneTableForSync(Table t) =>
        new()
        {
            Id = t.Id,
            UniqueId = t.UniqueId,
            TableNumber = t.TableNumber,
            Name = t.Name,
            Capacity = t.Capacity,
            Status = t.Status,
            AssignedServerId = t.AssignedServerId,
            AssignedServer = null
        };

    private static void StripOrderNav(OrderRecord o)
    {
        o.Table = null;
        o.Server = null;
        foreach (var i in o.Items)
        {
            i.OrderRecord = null;
            i.Product = null;
        }
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingTableId = null;
        IsTableNumberEditable = false;
    }

    private static int GetNextTableNumberFromNumbers(IEnumerable<int> usedNumbers)
    {
        var used = usedNumbers.ToHashSet();
        var next = 1;
        while (used.Contains(next))
            next++;
        return next;
    }
}
