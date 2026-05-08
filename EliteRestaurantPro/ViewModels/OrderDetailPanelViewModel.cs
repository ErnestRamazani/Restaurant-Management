using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;

namespace EliteRestaurantPro.ViewModels;

public sealed class OrderDetailLineRow
{
    public int Quantity { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Station { get; init; } = string.Empty;
    public string LineTotalText { get; init; } = string.Empty;
}

/// <summary>Slide-over order detail (lines, notes, totals) for any role.</summary>
public sealed class OrderDetailPanelViewModel : BaseViewModel
{
    private bool _isOpen;

    public OrderDetailPanelViewModel()
    {
        CloseCommand = new RelayCommand(_ => Close());
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>Kitchen/bar: hide line totals and grand total.</summary>
    public bool ShowPricing { get; private set; } = true;

    public string OrderCode { get; private set; } = string.Empty;
    public string TableLabel { get; private set; } = string.Empty;
    public string ServerName { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string CreatedText { get; private set; } = string.Empty;
    public string GrandTotalText { get; private set; } = string.Empty;
    public string CustomerNotes { get; private set; } = string.Empty;
    public string AllergyNotes { get; private set; } = string.Empty;

    public ObservableCollection<OrderDetailLineRow> Lines { get; } = new();

    public ICommand CloseCommand { get; }

    public void Load(int orderId, bool showPricing = true) => _ = LoadAsync(orderId, showPricing);

    public async Task LoadAsync(int orderId, bool showPricing = true)
    {
        ShowPricing = showPricing;
        OnPropertyChanged(nameof(ShowPricing));

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
            TableLabel = string.IsNullOrWhiteSpace(order.TableCode)
                ? $"Table {order.Table?.TableNumber ?? 0}"
                : $"{order.TableCode} · {order.TableName}";
            ServerName = string.IsNullOrWhiteSpace(order.ServerName)
                ? (order.Server?.Name ?? "Unassigned")
                : order.ServerName;
            Status = order.Status;
            CreatedText = order.CreatedAt.ToString("MMM d, yyyy · HH:mm");
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
                    Name = item.Product?.Name ?? "Item",
                    Station = string.IsNullOrWhiteSpace(item.PreparedByRole) ? "—" : item.PreparedByRole,
                    LineTotalText = showPricing ? $"$ {unit * item.Quantity:N2}" : string.Empty
                });
            }

            IsOpen = true;
            OnPropertyChanged(nameof(OrderCode));
            OnPropertyChanged(nameof(TableLabel));
            OnPropertyChanged(nameof(ServerName));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CreatedText));
            OnPropertyChanged(nameof(GrandTotalText));
            OnPropertyChanged(nameof(CustomerNotes));
            OnPropertyChanged(nameof(AllergyNotes));
        }
        catch
        {
            Close();
        }
    }

    public void Close()
    {
        IsOpen = false;
    }
}
