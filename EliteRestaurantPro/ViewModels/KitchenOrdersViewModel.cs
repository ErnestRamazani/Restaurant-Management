using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class KitchenOrderLineVm
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Station { get; init; } = string.Empty;
}

public sealed class KitchenOrdersViewModel : AdminBaseViewModel
{
    private readonly AdminDataApiClient _data = new();
    private readonly AdminOrderCloudOperations _cloudOps = new();
    private readonly List<OrderRecord> _ordersCache = [];
    private OrderEntry? _selectedIncoming;
    private OrderEntry? _selectedPreparing;
    private OrderEntry? _selectedReady;
    private bool _isLoading;
    private bool _hasDetail;

    public override string ActivePage => "KitchenQueue";

    public ObservableCollection<OrderEntry> IncomingOrders { get; } = new();
    public ObservableCollection<OrderEntry> PreparingOrders { get; } = new();
    public ObservableCollection<OrderEntry> ReadyPickupOrders { get; } = new();
    public ObservableCollection<KitchenOrderLineVm> DetailLines { get; } = new();

    public OrderEntry? SelectedIncoming
    {
        get => _selectedIncoming;
        set
        {
            if (!SetField(ref _selectedIncoming, value))
                return;
            if (value != null)
            {
                _selectedPreparing = null;
                _selectedReady = null;
                OnPropertyChanged(nameof(SelectedPreparing));
                OnPropertyChanged(nameof(SelectedReady));
            }

            LoadDetailForSelection();
        }
    }

    public OrderEntry? SelectedPreparing
    {
        get => _selectedPreparing;
        set
        {
            if (!SetField(ref _selectedPreparing, value))
                return;
            if (value != null)
            {
                _selectedIncoming = null;
                _selectedReady = null;
                OnPropertyChanged(nameof(SelectedIncoming));
                OnPropertyChanged(nameof(SelectedReady));
            }

            LoadDetailForSelection();
        }
    }

    public OrderEntry? SelectedReady
    {
        get => _selectedReady;
        set
        {
            if (!SetField(ref _selectedReady, value))
                return;
            if (value != null)
            {
                _selectedIncoming = null;
                _selectedPreparing = null;
                OnPropertyChanged(nameof(SelectedIncoming));
                OnPropertyChanged(nameof(SelectedPreparing));
            }

            LoadDetailForSelection();
        }
    }

    public string DetailOrderCode { get; private set; } = string.Empty;
    public string DetailTable { get; private set; } = string.Empty;
    public string DetailServer { get; private set; } = string.Empty;
    public string DetailStatus { get; private set; } = string.Empty;
    public string DetailTime { get; private set; } = string.Empty;
    public string DetailTotal { get; private set; } = string.Empty;
    public string DetailCustomerNotes { get; private set; } = string.Empty;
    public string DetailAllergyNotes { get; private set; } = string.Empty;

    public bool HasDetail
    {
        get => _hasDetail;
        private set => SetField(ref _hasDetail, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public OrderDetailPanelViewModel OrderDetail { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand StartPreparingCommand { get; }
    public ICommand MarkReadyForPickupCommand { get; }
    public ICommand ViewFullOrderCommand { get; }

    public KitchenOrdersViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        StartPreparingCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                _ = StartPreparingAsync(e);
        });
        MarkReadyForPickupCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                _ = MarkReadyForPickupAsync(e);
        });
        ViewFullOrderCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                OrderDetail.Load(e.Id, showPricing: false);
        });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        try
        {
            var ordersTask = _data.GetOrdersAsync();
            var productsTask = _data.GetProductsAsync();
            var tablesTask = _data.GetTablesAsync();
            var employeesTask = _data.GetEmployeesAsync();
            await Task.WhenAll(ordersTask, productsTask, tablesTask, employeesTask).ConfigureAwait(false);

            var orders = (await ordersTask.ConfigureAwait(false)).ToList();
            var productById = (await productsTask.ConfigureAwait(false)).ToDictionary(p => p.Id);
            var tableById = (await tablesTask.ConfigureAwait(false)).ToDictionary(t => t.Id);
            var empById = (await employeesTask.ConfigureAwait(false)).ToDictionary(e => e.Id);
            foreach (var o in orders)
            {
                if (o.Table is null && o.TableId is int tid && tableById.TryGetValue(tid, out var tbl))
                    o.Table = tbl;
                if (o.Server is null && o.ServerId is int sid && empById.TryGetValue(sid, out var emp))
                    o.Server = emp;
                foreach (var i in o.Items)
                {
                    if (i.Product is null && productById.TryGetValue(i.ProductId, out var p))
                        i.Product = p;
                }
            }

            _ordersCache.Clear();
            _ordersCache.AddRange(orders);

            var incoming = orders
                .Where(o => o.Status == "Waiting")
                .OrderBy(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            var preparing = orders
                .Where(o => o.Status == "In Kitchen")
                .OrderBy(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            var pickedUp = orders
                .Where(o => o.Status == "Ready")
                .OrderBy(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            IncomingOrders.Clear();
            foreach (var o in incoming)
                IncomingOrders.Add(o);

            PreparingOrders.Clear();
            foreach (var o in preparing)
                PreparingOrders.Add(o);

            ReadyPickupOrders.Clear();
            foreach (var o in pickedUp)
                ReadyPickupOrders.Add(o);

            var keepIn = _selectedIncoming is { Id: var inId } &&
                         incoming.Any(x => x.Id == inId);
            var keepPr = _selectedPreparing is { Id: var prId } &&
                         preparing.Any(x => x.Id == prId);
            var keepRd = _selectedReady is { Id: var rdId } &&
                         pickedUp.Any(x => x.Id == rdId);
            if (!keepIn)
            {
                _selectedIncoming = null;
                OnPropertyChanged(nameof(SelectedIncoming));
            }

            if (!keepPr)
            {
                _selectedPreparing = null;
                OnPropertyChanged(nameof(SelectedPreparing));
            }

            if (!keepRd)
            {
                _selectedReady = null;
                OnPropertyChanged(nameof(SelectedReady));
            }

            LoadDetailForSelection();
            RefreshReadyPickupBanner();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadDetailForSelection()
    {
        var entry = SelectedReady ?? SelectedPreparing ?? SelectedIncoming;
        if (entry is null)
        {
            ClearDetail();
            return;
        }

        var order = _ordersCache.FirstOrDefault(o => o.Id == entry.Id);
        if (order is null)
        {
            ClearDetail();
            return;
        }

        DetailOrderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
        DetailTable = string.IsNullOrWhiteSpace(order.TableCode)
            ? $"Table {order.Table?.TableNumber ?? 0}"
            : $"{order.TableCode} · {order.TableName}";
        DetailServer = string.IsNullOrWhiteSpace(order.ServerName) ? "—" : order.ServerName;
        DetailStatus = order.Status;
        DetailTime = order.CreatedAt.ToString("MMM d, yyyy · HH:mm");
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        DetailTotal = $"$ {totals.GrandTotal:N2}";
        DetailCustomerNotes = string.IsNullOrWhiteSpace(order.CustomerNotes) ? "—" : order.CustomerNotes.Trim();
        DetailAllergyNotes = string.IsNullOrWhiteSpace(order.AllergyNotes) ? "—" : order.AllergyNotes.Trim();

        DetailLines.Clear();
        foreach (var item in order.Items.OrderBy(i => i.Product?.Name))
        {
            DetailLines.Add(new KitchenOrderLineVm
            {
                ProductName = item.Product?.Name ?? "Item",
                Quantity = item.Quantity,
                Station = string.IsNullOrWhiteSpace(item.PreparedByRole) ? "—" : item.PreparedByRole
            });
        }

        HasDetail = true;
        OnPropertyChanged(nameof(DetailOrderCode));
        OnPropertyChanged(nameof(DetailTable));
        OnPropertyChanged(nameof(DetailServer));
        OnPropertyChanged(nameof(DetailStatus));
        OnPropertyChanged(nameof(DetailTime));
        OnPropertyChanged(nameof(DetailTotal));
        OnPropertyChanged(nameof(DetailCustomerNotes));
        OnPropertyChanged(nameof(DetailAllergyNotes));
    }

    private void ClearDetail()
    {
        DetailLines.Clear();
        DetailOrderCode = string.Empty;
        DetailTable = string.Empty;
        DetailServer = string.Empty;
        DetailStatus = string.Empty;
        DetailTime = string.Empty;
        DetailTotal = string.Empty;
        DetailCustomerNotes = string.Empty;
        DetailAllergyNotes = string.Empty;
        HasDetail = false;
        OnPropertyChanged(nameof(DetailOrderCode));
        OnPropertyChanged(nameof(DetailTable));
        OnPropertyChanged(nameof(DetailServer));
        OnPropertyChanged(nameof(DetailStatus));
        OnPropertyChanged(nameof(DetailTime));
        OnPropertyChanged(nameof(DetailTotal));
        OnPropertyChanged(nameof(DetailCustomerNotes));
        OnPropertyChanged(nameof(DetailAllergyNotes));
    }

    private async Task StartPreparingAsync(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Start preparing order {entry.OrderId}?\n\nStatus will move from Waiting to In Kitchen.",
            "Receive in kitchen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var status = _ordersCache.FirstOrDefault(o => o.Id == entry.Id)?.Status;
        if (status != "Waiting")
        {
            MessageBox.Show("Order is no longer waiting (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
            return;
        }

        var err = await _cloudOps.TryAdvanceOrderAsync(entry.Id).ConfigureAwait(false);
        if (err == string.Empty)
        {
            MessageBox.Show("Order is no longer waiting (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
            return;
        }

        if (err is not null)
        {
            MessageBox.Show(err, "Kitchen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshReadyPickupBanner();
        await LoadAsync();
    }

    private async Task MarkReadyForPickupAsync(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Mark order {entry.OrderId} ready for pickup?\n\nServer and cashier will see it in their pickup banner.",
            "Ready for pickup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var status = _ordersCache.FirstOrDefault(o => o.Id == entry.Id)?.Status;
        if (status != "In Kitchen")
        {
            MessageBox.Show("Order is no longer in kitchen (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
            return;
        }

        var err = await _cloudOps.TryAdvanceOrderAsync(entry.Id).ConfigureAwait(false);
        if (err == string.Empty)
        {
            MessageBox.Show("Order is no longer in kitchen (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
            return;
        }

        if (err is not null)
        {
            MessageBox.Show(err, "Kitchen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshReadyPickupBanner();
        await LoadAsync();
    }

    private static OrderEntry MapKitchenOrder(OrderRecord order)
    {
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        var items = string.Join(", ",
            order.Items.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));

        return new OrderEntry
        {
            Id = order.Id,
            OrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            TableNumber = string.IsNullOrWhiteSpace(order.TableCode)
                ? $"Table {order.Table?.TableNumber ?? 0}"
                : $"{order.TableCode} · {order.TableName}",
            ServerName = string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? "Unassigned")
                : order.ServerName,
            Items = items,
            CustomerNotes = order.CustomerNotes ?? string.Empty,
            AllergyNotes = order.AllergyNotes ?? string.Empty,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Time = order.CreatedAt.ToString("HH:mm"),
            Total = totals.GrandTotal,
            StatusColor = StatusColorFor(order.Status)
        };
    }

    private static string StatusColorFor(string status) => status switch
    {
        "Waiting" => "#2196F3",
        "In Kitchen" => "#FF9800",
        "Ready" => "#4CAF50",
        _ => "#D4AF37"
    };
}
