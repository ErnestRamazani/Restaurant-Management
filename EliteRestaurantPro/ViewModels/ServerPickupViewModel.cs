using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

/// <summary>Server tablet: Ready orders for this server → mark Served so cashier can complete.</summary>
public sealed class ServerPickupViewModel : AdminBaseViewModel
{
    private bool _isLoading;

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
                MarkServed(e);
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
            var list = await Task.Run(() =>
            {
                using var db = new AppDbContext();
                return db.Orders
                    .AsNoTracking()
                    .Include(o => o.Table)
                    .Include(o => o.Server)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Where(o => o.Status == "Ready" && o.ServerId == serverId)
                    .OrderBy(o => o.CreatedAt)
                    .ToList()
                    .Select(MapRow)
                    .ToList();
            });

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

    private void MarkServed(OrderEntry entry)
    {
        var confirm = MessageBox.Show(
            $"Mark order {entry.OrderId} as served to the table?\n\nIt will appear in Active orders as Served so the cashier can complete payment.",
            "Confirm served",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var order = db.Orders.FirstOrDefault(o =>
            o.Id == entry.Id && o.Status == "Ready" && o.ServerId == AppSession.StaffEmployeeId);
        if (order is null)
        {
            MessageBox.Show("Order is no longer Ready or not assigned to you.", "Pick up", MessageBoxButton.OK,
                MessageBoxImage.Information);
            _ = LoadAsync();
            return;
        }

        order.Status = OrderWorkflow.Served;
        db.SaveChanges();
        AppDbContext.ReconcileTableStatusesWithOrders(db);
        db.SaveChanges();
        OrderDetail.Close();
        _ = LoadAsync();
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
