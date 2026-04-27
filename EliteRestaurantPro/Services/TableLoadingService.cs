using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
using ModelTable = EliteRestaurant.Core.Models.Table;

namespace EliteRestaurantPro.Services;

public sealed record CreateOrderCatalogProduct(
    int ProductId,
    string UniqueId,
    string Name,
    string Category,
    string SubCategory,
    decimal Price);

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
    IReadOnlyList<string> DeliveryReferences);

/// <summary>Loads tables, menu, reservations, and delivery references for the create-order page.</summary>
public sealed class TableLoadingService
{
    public CreateOrderPageCatalog LoadCatalog(int? restrictToServerEmployeeId)
    {
        using var db = new AppDbContext();

        var tableQuery = db.Tables.AsNoTracking()
            .Include(t => t.AssignedServer)
            .Where(t => t.Status != "Maintenance" && t.AssignedServerId != null);
        if (restrictToServerEmployeeId.HasValue)
            tableQuery = tableQuery.Where(t => t.AssignedServerId == restrictToServerEmployeeId.Value);

        var tables = tableQuery.OrderBy(t => t.TableNumber).ToList();

        var products = db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SubCategory)
            .ThenBy(p => p.Name)
            .Select(p => new CreateOrderCatalogProduct(
                p.Id,
                p.UniqueId,
                p.Name,
                p.Category,
                string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory!,
                p.Price))
            .ToList();

        var arrivedReservations = db.Reservations
            .AsNoTracking()
            .Include(r => r.Table)
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
                r.Table != null && !string.IsNullOrWhiteSpace(r.Table.Name)
                    ? r.Table.Name
                    : (r.TableId.HasValue ? $"Table #{r.TableId.Value}" : "-")))
            .ToList();

        var deliveryReferences = db.Orders
            .AsNoTracking()
            .Where(o => o.OrderSource == "Delivery")
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => !string.IsNullOrWhiteSpace(o.ReservationGuestName)
                ? o.ReservationGuestName
                : (!string.IsNullOrWhiteSpace(o.TableName) ? o.TableName : o.UniqueId))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Take(40)
            .ToList();

        return new CreateOrderPageCatalog(tables, products, arrivedReservations, deliveryReferences);
    }
}
