using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class KitchenOrderLineVm
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Station { get; init; } = string.Empty;
    public bool IsNewForKitchen { get; init; }
    public bool IsAlreadyPrepared { get; init; }
    public string LineBadge { get; init; } = string.Empty;
}

public sealed class KitchenOrdersViewModel : AdminBaseViewModel
{
    private readonly AdminDataApiClient _data = new();
    private readonly AdminOrderCloudOperations _cloudOps = new();
    private readonly KitchenQueueHubClient _kitchenHub = new();
    private readonly List<OrderRecord> _ordersCache = [];
    private OrderEntry? _selectedIncoming;
    private OrderEntry? _selectedPreparing;
    private OrderEntry? _selectedReady;
    private bool _isLoading;
    private bool _hasDetail;
    private string _loadStatusMessage = string.Empty;
    private OrderEntry? _selectedKitchenEntry;
    private bool _showDetailReleaseToKitchen;
    private bool _showDetailReceiveInKitchen;
    private bool _showDetailMarkReady;
    private string _detailActionHint = string.Empty;
    private string _detailKitchenWorkSummary = string.Empty;

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

    public string LoadStatusMessage
    {
        get => _loadStatusMessage;
        private set
        {
            if (!SetField(ref _loadStatusMessage, value))
                return;
            OnPropertyChanged(nameof(HasLoadStatusMessage));
        }
    }

    public bool HasLoadStatusMessage => !string.IsNullOrWhiteSpace(LoadStatusMessage);

    public bool ShowDetailReleaseToKitchen
    {
        get => _showDetailReleaseToKitchen;
        private set => SetField(ref _showDetailReleaseToKitchen, value);
    }

    public bool ShowDetailReceiveInKitchen
    {
        get => _showDetailReceiveInKitchen;
        private set => SetField(ref _showDetailReceiveInKitchen, value);
    }

    public bool ShowDetailMarkReady
    {
        get => _showDetailMarkReady;
        private set => SetField(ref _showDetailMarkReady, value);
    }

    public string DetailActionHint
    {
        get => _detailActionHint;
        private set => SetField(ref _detailActionHint, value);
    }

    public bool HasDetailActionHint => !string.IsNullOrWhiteSpace(DetailActionHint);

    public string DetailKitchenWorkSummary
    {
        get => _detailKitchenWorkSummary;
        private set => SetField(ref _detailKitchenWorkSummary, value);
    }

    public bool HasDetailKitchenWorkSummary => !string.IsNullOrWhiteSpace(DetailKitchenWorkSummary);

    public OrderDetailPanelViewModel OrderDetail { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ReleaseToKitchenCommand { get; }
    public ICommand StartPreparingCommand { get; }
    public ICommand MarkReadyForPickupCommand { get; }
    public ICommand DetailReleaseToKitchenCommand { get; }
    public ICommand DetailReceiveInKitchenCommand { get; }
    public ICommand DetailMarkReadyCommand { get; }
    public ICommand ViewFullOrderCommand { get; }

    public KitchenOrdersViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        ReleaseToKitchenCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                _ = ReleaseToKitchenAsync(e);
        });
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
        DetailReleaseToKitchenCommand = new RelayCommand(_ =>
        {
            if (_selectedKitchenEntry is not null)
                _ = ReleaseToKitchenAsync(_selectedKitchenEntry);
        }, _ => _selectedKitchenEntry?.ShowReleaseToKitchen == true);
        DetailReceiveInKitchenCommand = new RelayCommand(_ =>
        {
            if (_selectedKitchenEntry is not null)
                _ = StartPreparingAsync(_selectedKitchenEntry);
        }, _ => _selectedKitchenEntry?.ShowReceiveInKitchen == true);
        DetailMarkReadyCommand = new RelayCommand(_ =>
        {
            if (_selectedKitchenEntry is not null)
                _ = MarkReadyForPickupAsync(_selectedKitchenEntry);
        }, _ => _selectedKitchenEntry?.ShowMarkReadyForPickup == true);
        ViewFullOrderCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                OrderDetail.Load(e.Id, showPricing: false);
        });
        _kitchenHub.QueueChanged += OnKitchenHubQueueChanged;
        _ = StartKitchenHubAndLoadAsync();
    }

    private void OnKitchenHubQueueChanged()
    {
        Application.Current?.Dispatcher.BeginInvoke(() => _ = LoadAsync());
    }

    private async Task StartKitchenHubAndLoadAsync()
    {
        try
        {
            await _kitchenHub.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            /* Live refresh is optional when hub is unreachable. */
        }

        await Application.Current.Dispatcher.InvokeAsync(() => _ = LoadAsync());
    }

    private async Task LoadAsync()
    {
        if (IsLoading)
            return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            IsLoading = true;
            LoadStatusMessage = string.Empty;
        });

        try
        {
            _data.ReloadFromSettings();
            var ordersTask = _data.GetOrdersAsync();
            var productsTask = _data.GetProductsAsync();
            var tablesTask = _data.GetTablesAsync();
            var employeesTask = _data.GetEmployeesAsync();
            await Task.WhenAll(ordersTask, productsTask, tablesTask, employeesTask).ConfigureAwait(false);

            var orders = (await ordersTask.ConfigureAwait(false))
                .Where(o => OrderWorkflow.IsKitchenKdsVisibleStatus(o.Status))
                .ToList();
            var productById = (await productsTask.ConfigureAwait(false)).ToDictionary(p => p.Id);
            var tableById = (await tablesTask.ConfigureAwait(false)).ToDictionary(t => t.Id);
            var empById = (await employeesTask.ConfigureAwait(false)).ToDictionary(e => e.Id);
            foreach (var o in orders)
            {
                if (o.Table is null && o.TableId is int tid && tableById.TryGetValue(tid, out var tbl))
                    o.Table = tbl;
                if (o.Server is null && o.ServerId is int sid && empById.TryGetValue(sid, out var emp))
                    o.Server = emp;
                foreach (var i in o.Items ?? [])
                {
                    if (i.Product is null && productById.TryGetValue(i.ProductId, out var p))
                        i.Product = p;
                }
            }

            var incoming = orders
                .Where(o => OrderWorkflow.IsKitchenIncomingColumn(o.Status))
                .OrderByDescending(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            var preparing = orders
                .Where(o => OrderWorkflow.IsKitchenPreparingColumn(o.Status))
                .OrderBy(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            var pickedUp = orders
                .Where(o => OrderWorkflow.IsKitchenReadyColumn(o.Status))
                .OrderBy(o => o.CreatedAt)
                .Select(MapKitchenOrder)
                .ToList();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _ordersCache.Clear();
                _ordersCache.AddRange(orders);

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

                LoadStatusMessage =
                    orders.Count == 0
                        ? "No orders returned from the cloud API. Check cloud URL and sign-in, then tap Refresh."
                        : incoming.Count + preparing.Count + pickedUp.Count == 0
                            ? $"Loaded {orders.Count} order(s) from cloud — none are in the kitchen queue yet."
                            : string.Empty;

                LoadDetailForSelection();
                RefreshReadyPickupBanner();
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LoadStatusMessage = $"Could not load kitchen tickets: {ex.Message}";
            });
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() => IsLoading = false);
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
        DetailTable = OrderRecordUiLabels.TableCaption(order);
        DetailServer = OrderRecordUiLabels.ServerCaption(order);
        DetailStatus = order.Status;
        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;
        DetailTime = RestaurantTimeZone.FormatOrderCreatedAt(order.CreatedAt, tz);
        var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        DetailTotal = $"$ {totals.GrandTotal:N2}";
        var kitchenCust = KitchenCustomerNotesDisplay.ForKitchen(order);
        DetailCustomerNotes = string.IsNullOrWhiteSpace(kitchenCust) ? "—" : kitchenCust;
        DetailAllergyNotes = string.IsNullOrWhiteSpace(order.AllergyNotes) ? "—" : order.AllergyNotes.Trim();

        var lineItems = order.Items?.ToList() ?? [];
        var work = KitchenLineVisibility.Summarize(lineItems);
        DetailKitchenWorkSummary = work.CardSummaryText;

        DetailLines.Clear();
        foreach (var item in lineItems.OrderBy(i => i.Product?.Name))
        {
            var isNew = KitchenLineVisibility.IsNewForKitchen(item, lineItems);
            var prepared = KitchenLineVisibility.IsLinePrepared(item);
            DetailLines.Add(new KitchenOrderLineVm
            {
                ProductName = item.Product?.Name ?? "Item",
                Quantity = item.Quantity,
                Station = string.IsNullOrWhiteSpace(item.PreparedByRole) ? "—" : item.PreparedByRole,
                IsNewForKitchen = isNew,
                IsAlreadyPrepared = prepared,
                LineBadge = isNew ? "NEW" : prepared ? "Prepared" : string.Empty
            });
        }

        _selectedKitchenEntry = entry;
        ApplyDetailKitchenActions(entry, order.Status, work);

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

    private void ApplyDetailKitchenActions(OrderEntry entry, string status, KitchenWorkSummary work)
    {
        var key = OrderWorkflow.KitchenStatusKey(status);
        ShowDetailReleaseToKitchen = false;
        ShowDetailReceiveInKitchen = entry.ShowReceiveInKitchen;
        ShowDetailMarkReady = entry.ShowMarkReadyForPickup;

        var newWorkHint = work.HighlightNewOnTicket
            ? "Prepare NEW lines only — already-prepared items do not need re-cooking."
            : string.Empty;

        DetailActionHint = key switch
        {
            "waiting" or "inKitchen" => newWorkHint,
            "ready" =>
                "This ticket is ready for pickup. Servers or cashier can complete it from their screens.",
            "served" or "other" =>
                "No kitchen action for this status. Refresh if the ticket changed.",
            _ => newWorkHint
        };

        OnPropertyChanged(nameof(HasDetailActionHint));
    }

    private void ClearDetail()
    {
        _selectedKitchenEntry = null;
        ShowDetailReleaseToKitchen = false;
        ShowDetailReceiveInKitchen = false;
        ShowDetailMarkReady = false;
        DetailActionHint = string.Empty;
        DetailKitchenWorkSummary = string.Empty;
        OnPropertyChanged(nameof(HasDetailActionHint));
        OnPropertyChanged(nameof(HasDetailKitchenWorkSummary));

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

    private async Task ReleaseToKitchenAsync(OrderEntry entry)
    {
        var order = _ordersCache.FirstOrDefault(o => o.Id == entry.Id);
        if (order is null || !OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen(order.Status))
        {
            MessageBox.Show("Order is no longer pending release (refresh the list).", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
            return;
        }

        var confirm = MessageBox.Show(
            $"Approve and release order {entry.OrderId} to the kitchen?\n\nInventory will be deducted and the ticket moves to Waiting.",
            "Release to kitchen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var result = await _cloudOps.TryReleasePendingToKitchenAsync(entry.Id).ConfigureAwait(false);
        if (!result.Ok)
        {
            MessageBox.Show(result.ErrorMessage ?? "Release failed.", "Kitchen", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        RefreshReadyPickupBanner();
        await LoadAsync();
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
        if (OrderWorkflow.KitchenStatusKey(status) != "waiting")
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
        if (!OrderWorkflow.IsKitchenPreparingColumn(status))
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
        var lines = order.Items?.ToList() ?? [];
        var lineSubtotal = lines.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
        var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
        var items = string.Join(", ",
            lines.Select(i => $"{i.Product?.Name ?? "Unknown"} x{i.Quantity}"));

        var awaitsRelease = OrderWorkflow.AwaitsCashierOrApprovalBeforeKitchen(order.Status);
        var work = KitchenLineVisibility.Summarize(lines);
        var tz = SettingsManager.Load().BusinessProfile.RestaurantTimeZoneId;
        return new OrderEntry
        {
            Id = order.Id,
            OrderId = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId,
            TableNumber = OrderRecordUiLabels.TableCaption(order),
            ServerName = OrderRecordUiLabels.ServerCaption(order),
            Items = items,
            KitchenWorkSummary = work.CardSummaryText,
            CustomerNotes = order.CustomerNotes ?? string.Empty,
            AllergyNotes = order.AllergyNotes ?? string.Empty,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Time = RestaurantTimeZone.FormatUtc(order.CreatedAt, tz, "HH:mm"),
            Total = totals.GrandTotal,
            StatusColor = StatusColorFor(order.Status),
            OrderOrigin = order.OrderOrigin,
            FulfillmentHeadline = OrderRecordUiLabels.KitchenFulfillmentHeadline(order),
            ShowReleaseToKitchen = false,
            ShowReceiveInKitchen = OrderWorkflow.KitchenStatusKey(order.Status) == "waiting",
            ShowMarkReadyForPickup = OrderWorkflow.IsKitchenPreparingColumn(order.Status)
        };
    }

    private static string StatusColorFor(string status)
    {
        if (OrderWorkflow.IsPendingApproval(status) || OrderWorkflow.IsPendingCashier(status))
            return "#B39DDB";
        return status switch
        {
            "Waiting" => "#2196F3",
            "In Kitchen" => "#FF9800",
            "Ready" => "#4CAF50",
            _ => "#D4AF37"
        };
    }
}
