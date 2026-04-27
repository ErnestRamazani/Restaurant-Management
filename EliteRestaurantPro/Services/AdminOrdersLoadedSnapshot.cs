using EliteRestaurant.Core.Models;
using EliteRestaurantPro.ViewModels;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.Services;

public sealed class AdminOrdersLoadedSnapshot
{
    public List<CashierQueueRow> PendingCashier { get; init; } = [];
    public List<OrderEntry> ActiveOrders { get; init; } = [];
    public List<OrderEntry> PastOrders { get; init; } = [];
    public List<ModelTable> AvailableTables { get; init; } = [];
    public List<ProductSelectionItemViewModel> ProductSelections { get; init; } = [];
}
