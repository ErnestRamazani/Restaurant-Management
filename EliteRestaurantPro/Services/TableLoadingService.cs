using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurantPro.ApiClients;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.Services;

public sealed record CreateOrderCatalogProduct(
    int ProductId,
    string UniqueId,
    string Name,
    string Category,
    string SubCategory,
    decimal Price,
    int PrepMinutes,
    bool IsAvailable);

public sealed record CreateOrderArrivedReservationRow(
    int Id,
    string UniqueId,
    string ReservationName,
    string GuestName,
    DateTime ReservedFor,
    int? TableId,
    string TableLabel);

public sealed record CreateOrderPageCatalog(
    IReadOnlyList<ModelTable> Tables,
    IReadOnlyList<CreateOrderCatalogProduct> Products,
    IReadOnlyList<CreateOrderArrivedReservationRow> ArrivedReservations,
    IReadOnlyList<string> DeliveryReferences,
    IReadOnlyList<OrderRecord> OrdersSnapshot);

/// <summary>Loads tables, menu, reservations, and delivery references for the create-order page.</summary>
public sealed class TableLoadingService
{
    private readonly AdminDataApiClient _dataClient = new();

    public async Task<CreateOrderPageCatalog> LoadCatalogAsync(int? restrictToServerEmployeeId, CancellationToken cancellationToken = default)
    {
        var (allTables, allProducts, allReservations, allOrders) =
            await _dataClient.GetCreateOrderCatalogAsync(cancellationToken).ConfigureAwait(false);

        var tableQuery = allTables
            .Where(t => t.Status != "Maintenance" && t.AssignedServerId != null);
        if (restrictToServerEmployeeId.HasValue)
            tableQuery = tableQuery.Where(t => t.AssignedServerId == restrictToServerEmployeeId.Value);

        var tables = tableQuery.OrderBy(t => t.TableNumber).ToList();

        var products = allProducts
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .Select(p => new CreateOrderCatalogProduct(
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory!,
                p.Price,
                Math.Max(0, p.PrepMinutes),
                p.IsAvailable))
            .ToList();

        var tableNames = tables.ToDictionary(t => t.Id, t => string.IsNullOrWhiteSpace(t.Name) ? $"Table #{t.Id}" : t.Name);
        var arrivedReservations = allReservations
            .Where(r => r.Status == "Arrived")
            .OrderBy(r => r.ReservedFor)
            .Take(60)
            .Select(r => new CreateOrderArrivedReservationRow(
                r.Id,
                r.UniqueId,
                r.ReservationName,
                r.GuestName,
                r.ReservedFor,
                r.TableId,
                r.TableId.HasValue && tableNames.TryGetValue(r.TableId.Value, out var tableName)
                    ? tableName
                    : (r.TableId.HasValue ? $"Table #{r.TableId.Value}" : "-")))
            .ToList();

        var deliveryReferences = allOrders
            .Where(o => o.OrderSource == "Delivery")
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => !string.IsNullOrWhiteSpace(o.ReservationGuestName)
                ? o.ReservationGuestName
                : (!string.IsNullOrWhiteSpace(o.TableName) ? o.TableName : o.UniqueId))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Take(40)
            .ToList();

        return new CreateOrderPageCatalog(tables, products, arrivedReservations, deliveryReferences, allOrders);
    }
}
