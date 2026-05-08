using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

namespace EliteRestaurantPro.ViewModels;

/// <summary>Server tablet: Ready orders for this server → mark Served so cashier can complete.</summary>
public sealed class ServerPickupViewModel : AdminBaseViewModel
{
    private bool _isLoading;
    private readonly AdminDataApiClient _data = new();
    private readonly AdminOrderCloudOperations _cloudOps = new();

    public override string ActivePage => "ServerPickup";

    public ObservableCollection<OrderEntry> ReadyOrders { get; } = new();
    public OrderDetailPanelViewModel OrderDetail { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ViewOrderCommand { get; }
    public ICommand MarkServedCommand { get; }

    public ServerPickupViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        ViewOrderCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                OrderDetail.Load(e.Id);
        });
        MarkServedCommand = new RelayCommand(p =>
        {
            if (p is OrderEntry e)
                _ = MarkServedAsync(e);
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
            var serverId = AppSession.StaffEmployeeId;
            var orders = (await _data.GetOrdersAsync().ConfigureAwait(false)).ToList();
            var tables = (await _data.GetTablesAsync().ConfigureAwait(false)).ToList();
            var employees = (await _data.GetEmployeesAsync().ConfigureAwait(false)).ToList();
            var products = (await _data.GetProductsAsync().ConfigureAwait(false)).ToList();
            var productById = products.ToDictionary(p => p.Id);
            var tablesById = tables.ToDictionary(t => t.Id);
            var empById = employees.ToDictionary(e => e.Id);

            foreach (var o in orders)
            {
                if (o.TableId is int tid && tablesById.TryGetValue(tid, out var tbl))
                    o.Table = tbl;
                if (o.ServerId is int sid && empById.TryGetValue(sid, out var emp))
                    o.Server = emp;
                foreach (var item in o.Items)
                {
                    if (productById.TryGetValue(item.ProductId, out var p))
                        item.Product = p;
                }
            }

            var list = orders
                .Where(o => o.Status == "Ready" && o.ServerId == serverId)
                .OrderBy(o => o.CreatedAt)
                .Select(MapRow)
                .ToList();

            ReadyOrders.Clear();
            foreach (var o in list)
                ReadyOrders.Add(o);

            RefreshReadyPickupBanner();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task MarkServedAsync(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Mark order {entry.OrderId} as served to the table?\n\nIt will appear in Active orders as Served so the cashier can complete payment.",
            "Confirm served",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var orders = (await _data.GetOrdersAsync().ConfigureAwait(false)).ToList();
            var order = orders.FirstOrDefault(o =>
                o.Id == entry.Id && o.Status == "Ready" && o.ServerId == AppSession.StaffEmployeeId);
            if (order is null)
            {
                MessageBox.Show("Order is no longer Ready or not assigned to you.", "Pick up", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                await LoadAsync();
                return;
            }

            var msg = await _cloudOps.TryAdvanceOrderAsync(entry.Id).ConfigureAwait(true);
            if (msg is not null && msg != string.Empty)
            {
                MessageBox.Show(msg, "Pick up", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Pick up", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        OrderDetail.Close();
        await LoadAsync();
    }

    private static OrderEntry MapRow(OrderRecord order)
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
            StatusColor = "#4CAF50"
        };
    }
}
