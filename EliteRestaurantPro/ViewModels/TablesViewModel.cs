using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public class TablesViewModel : AdminBaseViewModel
{
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
        DeleteTableCommand = new RelayCommand(table => DeleteTable(table as Table));
        SaveTableCommand = new RelayCommand(_ => SaveTable());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        LoadTables();
    }

    private void LoadTables()
    {
        Tables.Clear();
        Servers.Clear();

        using var db = new AppDbContext();
        foreach (var server in db.Employees.AsNoTracking().Where(e => e.Role.ToLower() == "server").OrderBy(e => e.Name))
        {
            Servers.Add(server);
        }

        _allTables.Clear();
        foreach (var table in db.Tables
                     .AsNoTracking()
                     .Include(t => t.AssignedServer)
                     .OrderBy(t => t.TableNumber))
        {
            _allTables.Add(table);
        }

        ApplyTablesFilter();
        RefreshReadyPickupBanner();
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

    private void SaveTable()
    {
        if (AppSession.IsStaffTablet) return;

        if (!int.TryParse(CapacityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity) ||
            string.IsNullOrWhiteSpace(SelectedStatus) ||
            string.IsNullOrWhiteSpace(TableNameText))
        {
            return;
        }

        using var db = new AppDbContext();
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

        if (normalizedStatus == "Maintenance" && _editingTableId is int maintenanceTableId)
        {
            var activeOrdersExist = db.Orders.Any(o => o.TableId == maintenanceTableId &&
                (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                 o.Status == OrderWorkflow.Served ||
                 o.Status == OrderWorkflow.PendingCashier));
            if (activeOrdersExist)
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
            var activeOrdersExist = db.Orders.Any(o => o.TableId == availableTableId &&
                (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
                 o.Status == OrderWorkflow.Served ||
                 o.Status == OrderWorkflow.PendingCashier));
            if (activeOrdersExist)
            {
                MessageBox.Show(
                    "This table has active orders and cannot be set to Available yet.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            if (_editingTableId is int tableId)
            {
                if (!int.TryParse(TableNumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var editedTableNumber))
                    return;

                var existing = db.Tables.Single(t => t.Id == tableId);
                existing.TableNumber = editedTableNumber;
                existing.Name = TableNameText.Trim();
                existing.Capacity = capacity;
                existing.Status = normalizedStatus;
                existing.AssignedServerId = SelectedServerId;
            }
            else
            {
                var nextTableNumber = GetNextTableNumber(db);
                var confirmAdd = MessageBox.Show(
                    $"Add this table as ID {nextTableNumber}?",
                    "Confirm Add Table",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmAdd != MessageBoxResult.Yes)
                    return;

                db.Tables.Add(new Table
                {
                    UniqueId = UniqueIdGenerator.NewId("TBL"),
                    TableNumber = nextTableNumber,
                    Name = TableNameText.Trim(),
                    Capacity = capacity,
                    Status = normalizedStatus,
                    AssignedServerId = SelectedServerId
                });
            }

            db.SaveChanges();
            CloseDialog();
            LoadTables();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            MessageBox.Show(
                "A table with this ID already exists. Table IDs must be unique.",
                "Duplicate Table ID",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void DeleteTable(Table? table)
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

        using var db = new AppDbContext();
        var existing = db.Tables.SingleOrDefault(t => t.Id == table.Id);
        if (existing is null) return;

        var activeOrdersExist = db.Orders.Any(o =>
            o.TableId == existing.Id &&
            (o.Status == "Waiting" || o.Status == "In Kitchen" || o.Status == "Ready" ||
             o.Status == OrderWorkflow.Served ||
             o.Status == OrderWorkflow.PendingCashier));

        if (activeOrdersExist)
        {
            MessageBox.Show(
                "This table has active orders and cannot be deleted.",
                "Delete Blocked",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var pastOrders = db.Orders.Where(o =>
            o.TableId == existing.Id &&
            (o.Status == "Completed" || o.Status == "Cancelled"));

        foreach (var order in pastOrders)
        {
            order.TableCode = $"Table {existing.TableNumber}";
            order.TableName = string.IsNullOrWhiteSpace(existing.Name) ? $"Table {existing.TableNumber}" : existing.Name;
            order.TableId = null;
        }

        db.Tables.Remove(existing);
        db.SaveChanges();
        LoadTables();
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingTableId = null;
        IsTableNumberEditable = false;
    }

    private static int GetNextTableNumber(AppDbContext db)
    {
        var used = db.Tables.AsNoTracking().Select(t => t.TableNumber).ToHashSet();
        var next = 1;
        while (used.Contains(next))
            next++;
        return next;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
