using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public sealed class KitchenOrderLineVm
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Station { get; init; } = string.Empty;
}

public sealed class KitchenOrdersViewModel : AdminBaseViewModel
{
    private OrderEntry? _selectedIncoming;
    private OrderEntry? _selectedPreparing;
    private bool _isLoading;
    private bool _hasDetail;

    public override string ActivePage => "KitchenQueue";

    public ObservableCollection<OrderEntry> IncomingOrders { get; } = new();
    public ObservableCollection<OrderEntry> PreparingOrders { get; } = new();
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
                OnPropertyChanged(nameof(SelectedPreparing));
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
                OnPropertyChanged(nameof(SelectedIncoming));
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
                StartPreparing(e);
        });
        MarkReadyForPickupCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                MarkReadyForPickup(e);
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
            var (incoming, preparing) = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                var incomingList = db.Orders
                    .AsNoTracking()
                    .Include(o => o.Table)
                    .Include(o => o.Server)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == "Waiting")
                    .OrderBy(o => o.CreatedAt)
                    .ToList()
                    .Select(MapKitchenOrder)
                    .ToList();

                var preparingList = db.Orders
                    .AsNoTracking()
                    .Include(o => o.Table)
                    .Include(o => o.Server)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == "In Kitchen")
                    .OrderBy(o => o.CreatedAt)
                    .ToList()
                    .Select(MapKitchenOrder)
                    .ToList();

                return (incomingList, preparingList);
            });

            IncomingOrders.Clear();
            foreach (var o in incoming)
                IncomingOrders.Add(o);

            PreparingOrders.Clear();
            foreach (var o in preparing)
                PreparingOrders.Add(o);

            // Keep selection if still present
            var keepIn = _selectedIncoming is { Id: var inId } &&
                         incoming.Any(x => x.Id == inId);
            var keepPr = _selectedPreparing is { Id: var prId } &&
                         preparing.Any(x => x.Id == prId);
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
        var entry = SelectedPreparing ?? SelectedIncoming;
        if (entry is null)
        {
            ClearDetail();
            return;
        }

        using var db = new AppDbContext();
        var order = db.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.Id == entry.Id);

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

    private void StartPreparing(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Start preparing order {entry.OrderId}?\n\nStatus will move from Waiting to In Kitchen.",
            "Receive in kitchen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var order = db.Orders.FirstOrDefault(o => o.Id == entry.Id && o.Status == "Waiting");
        if (order is null)
        {
            MessageBox.Show("Order is no longer waiting (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadAsync();
            return;
        }

        order.Status = "In Kitchen";
        db.SaveChanges();
        RefreshReadyPickupBanner();
        _ = LoadAsync();
    }

    private void MarkReadyForPickup(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Mark order {entry.OrderId} ready for pickup?\n\nServer and cashier will see it in their pickup banner.",
            "Ready for pickup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var order = db.Orders.FirstOrDefault(o => o.Id == entry.Id && o.Status == "In Kitchen");
        if (order is null)
        {
            MessageBox.Show("Order is no longer in kitchen (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadAsync();
            return;
        }

        order.Status = "Ready";
        db.SaveChanges();
        RefreshReadyPickupBanner();
        _ = LoadAsync();
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
