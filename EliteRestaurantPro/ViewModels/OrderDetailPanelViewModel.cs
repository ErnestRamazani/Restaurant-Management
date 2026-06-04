using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

public sealed class OrderDetailLineRow
{
    public int Quantity { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Station { get; init; } = string.Empty;
    public string StationLine => string.IsNullOrWhiteSpace(Station) ? "—" : Station;
    public string LineTotalText { get; init; } = string.Empty;
}

/// <summary>Slide-over order detail (lines, notes, totals) for any role.</summary>
public sealed class OrderDetailPanelViewModel : LocalizableViewModel
{
    private bool _isOpen;
    private bool _showPricing = true;
    private string _orderCode = string.Empty;
    private string _grandTotalText = string.Empty;
    private string _customerNotes = string.Empty;
    private string _allergyNotes = string.Empty;
    private string _viewOrderTitle = string.Empty;
    private string _closeLabel = string.Empty;
    private string _linesLabel = string.Empty;
    private string _customerNotesLabel = string.Empty;
    private string _allergyNotesLabel = string.Empty;
    private string _totalPrefix = string.Empty;
    private string _displayStatus = string.Empty;
    private string _displayTableLabel = string.Empty;
    private string _displayCreatedText = string.Empty;
    private string _serverLine = string.Empty;
    private string _packagingBannerLine = string.Empty;

    public OrderDetailPanelViewModel()
    {
        CloseCommand = new RelayCommand(_ => Close());
        OrderDetailUiLocalizer.Apply(this);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>Kitchen/bar: hide line totals and grand total.</summary>
    public bool ShowPricing
    {
        get => _showPricing;
        private set => SetField(ref _showPricing, value);
    }

    public string OrderCode
    {
        get => _orderCode;
        private set => SetField(ref _orderCode, value);
    }

    public string GrandTotalText
    {
        get => _grandTotalText;
        private set => SetField(ref _grandTotalText, value);
    }

    public string CustomerNotes
    {
        get => _customerNotes;
        private set => SetField(ref _customerNotes, value);
    }

    public string AllergyNotes
    {
        get => _allergyNotes;
        private set => SetField(ref _allergyNotes, value);
    }

    public string ViewOrderTitle
    {
        get => _viewOrderTitle;
        set => SetField(ref _viewOrderTitle, value);
    }

    public string CloseLabel
    {
        get => _closeLabel;
        set => SetField(ref _closeLabel, value);
    }

    public string LinesLabel
    {
        get => _linesLabel;
        set => SetField(ref _linesLabel, value);
    }

    public string CustomerNotesLabel
    {
        get => _customerNotesLabel;
        set => SetField(ref _customerNotesLabel, value);
    }

    public string AllergyNotesLabel
    {
        get => _allergyNotesLabel;
        set => SetField(ref _allergyNotesLabel, value);
    }

    public string TotalPrefix
    {
        get => _totalPrefix;
        set => SetField(ref _totalPrefix, value);
    }

    public string DisplayStatus
    {
        get => _displayStatus;
        set => SetField(ref _displayStatus, value);
    }

    public string DisplayTableLabel
    {
        get => _displayTableLabel;
        set => SetField(ref _displayTableLabel, value);
    }

    public string DisplayCreatedText
    {
        get => _displayCreatedText;
        set => SetField(ref _displayCreatedText, value);
    }

    public string ServerLine
    {
        get => _serverLine;
        set => SetField(ref _serverLine, value);
    }

    public string PackagingBannerLine
    {
        get => _packagingBannerLine;
        set => SetField(ref _packagingBannerLine, value);
    }

    public bool ShowPackagingBanner => !string.IsNullOrWhiteSpace(PackagingBannerLine);

    public string RawStatus { get; private set; } = string.Empty;
    public string RawServerName { get; private set; } = string.Empty;
    public string RawTableCaption { get; private set; } = string.Empty;
    public string RawTableCode { get; private set; } = string.Empty;
    public string RawTableName { get; private set; } = string.Empty;
    public int RawTableNumber { get; private set; }
    public bool HasTableCode { get; private set; }
    public DateTime RawCreatedAtUtc { get; private set; }
    public bool RawPackagingRequired { get; private set; }

    public ObservableCollection<OrderDetailLineRow> Lines { get; } = new();

    public ICommand CloseCommand { get; }

    public void Load(int orderId, bool showPricing = true) => _ = LoadAsync(orderId, showPricing);

    public async Task LoadAsync(int orderId, bool showPricing = true)
    {
        ShowPricing = showPricing;

        try
        {
            var data = new AdminDataApiClient();
            var ordersTask = data.GetOrdersAsync();
            var tablesTask = data.GetTablesAsync();
            var employeesTask = data.GetEmployeesAsync();
            var productsTask = data.GetProductsAsync();
            await Task.WhenAll(ordersTask, tablesTask, employeesTask, productsTask).ConfigureAwait(true);
            var order = (await ordersTask.ConfigureAwait(true)).FirstOrDefault(x => x.Id == orderId);
            var tables = (await tablesTask.ConfigureAwait(true)).ToList();
            var employees = (await employeesTask.ConfigureAwait(true)).ToList();
            var products = (await productsTask.ConfigureAwait(true)).ToDictionary(p => p.Id);

            if (order is null)
            {
                Close();
                return;
            }

            foreach (var item in order.Items)
            {
                if (products.TryGetValue(item.ProductId, out var p))
                    item.Product = p;
            }

            if (order.TableId is int tid)
                order.Table = tables.FirstOrDefault(t => t.Id == tid);
            if (order.ServerId is int sid)
                order.Server = employees.FirstOrDefault(e => e.Id == sid);

            OrderCode = string.IsNullOrWhiteSpace(order.UniqueId) ? $"#{order.Id:000}" : order.UniqueId;
            RawTableCaption = OrderRecordUiLabels.TableCaption(order);
            RawTableCode = order.TableCode ?? string.Empty;
            RawTableName = order.TableName ?? string.Empty;
            RawTableNumber = order.Table?.TableNumber ?? 0;
            HasTableCode = !string.IsNullOrWhiteSpace(order.TableCode);
            RawServerName = string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? Loc.Admin("ordServerUnassigned", "Unassigned"))
                : order.ServerName;
            RawStatus = OrderDisplayStatus.ForOrder(order);
            RawCreatedAtUtc = order.CreatedAt;
            RawPackagingRequired = KitchenTicketPackaging.IsOnlinePackagingOrder(order);

            var lineSubtotal = order.Items.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
            var totals = OrderTotalsHelper.ComputeTotals(lineSubtotal, order.DiscountMode, order.DiscountValue);
            GrandTotalText = showPricing ? $"$ {totals.GrandTotal:N2}" : string.Empty;
            CustomerNotes = string.IsNullOrWhiteSpace(order.CustomerNotes) ? "—" : order.CustomerNotes.Trim();
            AllergyNotes = string.IsNullOrWhiteSpace(order.AllergyNotes) ? "—" : order.AllergyNotes.Trim();

            Lines.Clear();
            foreach (var item in order.Items.OrderBy(i => i.Product?.Name))
            {
                var unit = item.Product?.Price ?? 0m;
                Lines.Add(new OrderDetailLineRow
                {
                    Quantity = item.Quantity,
                    Name = item.Product?.Name ?? Loc.Admin("ordDetailItemFallback", "Item"),
                    Station = string.IsNullOrWhiteSpace(item.PreparedByRole) ? "—" : item.PreparedByRole,
                    LineTotalText = showPricing ? $"$ {unit * item.Quantity:N2}" : string.Empty
                });
            }

            OrderDetailUiLocalizer.Apply(this);
            OnPropertyChanged(nameof(ShowPackagingBanner));
            IsOpen = true;
        }
        catch
        {
            Close();
        }
    }

    protected override void RefreshLocalizedStrings()
    {
        OrderDetailUiLocalizer.Apply(this);
        OnPropertyChanged(nameof(ShowPackagingBanner));
    }

    public void Close()
    {
        IsOpen = false;
    }
}
