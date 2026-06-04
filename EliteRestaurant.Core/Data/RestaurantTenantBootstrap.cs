using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Tenancy;
using System.Linq;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>Creates the default tenant and backfills <see cref="IRestaurantScoped"/> rows on existing databases.</summary>
public static class RestaurantTenantBootstrap
{
    public const string DefaultSlug = "etoile-gourmande";
    public const string DefaultUniqueId = "REST-DEFAULT-001";
    public const string DefaultDomain = "etoilegourmandekin.com";

    /// <summary>Backfills <see cref="IRestaurantScoped.RestaurantId"/> on legacy rows. Does not create tenants (use setup API).</summary>
    public static void EnsureDefaultRestaurant(AppDbContext db)
    {
        var restaurant = db.Restaurants.IgnoreQueryFilters().OrderBy(r => r.Id).FirstOrDefault();
        if (restaurant is null)
            return;

        BackfillRestaurantId(db, restaurant.Id);
    }

    private static void BackfillRestaurantId(AppDbContext db, int restaurantId)
    {
        if (!db.Database.IsRelational())
        {
            BackfillRestaurantIdInMemory(db, restaurantId);
            return;
        }

        db.Database.ExecuteSqlRaw("""UPDATE "Employees" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "Products" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        BackfillTablesRestaurantId(db, restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "Orders" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "InventoryItems" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "CustomerProfiles" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "Reservations" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "PlacementUnits" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "ReservationEngagements" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "WaitlistEntries" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "SharedOrderDrafts" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "PublicMenuSettings" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "PublicMenuAssets" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "Transactions" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "SyncOutbox" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "RestaurantClients" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
        db.Database.ExecuteSqlRaw("""UPDATE "ClientDebtLedgerEntries" SET "RestaurantId" = {0} WHERE "RestaurantId" = 0 OR "RestaurantId" IS NULL""", restaurantId);
    }

    private static void BackfillRestaurantIdInMemory(AppDbContext db, int restaurantId)
    {
        StampIfUnset(db.Employees.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.Products.IgnoreQueryFilters(), restaurantId);
        BackfillTablesRestaurantId(db, restaurantId);
        StampIfUnset(db.Orders.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.InventoryItems.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.CustomerProfiles.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.Reservations.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.PlacementUnits.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.ReservationEngagements.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.WaitlistEntries.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.SharedOrderDrafts.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.PublicMenuSettings.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.PublicMenuAssets.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.Transactions.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.SyncOutbox.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.RestaurantClients.IgnoreQueryFilters(), restaurantId);
        StampIfUnset(db.ClientDebtLedgerEntries.IgnoreQueryFilters(), restaurantId);
        db.SaveChanges();
    }

    private static void StampIfUnset<T>(IQueryable<T> query, int restaurantId)
        where T : class, IRestaurantScoped
    {
        foreach (var row in query.Where(e => e.RestaurantId == 0).ToList())
            row.RestaurantId = restaurantId;
    }

    /// <summary>
    /// Legacy rows may have <c>RestaurantId = 0</c> while another row already uses the same
    /// <see cref="Table.TableNumber"/> for the target restaurant — a bulk UPDATE would violate
    /// <c>IX_Tables_RestaurantId_TableNumber</c>.
    /// </summary>
    private static void BackfillTablesRestaurantId(AppDbContext db, int restaurantId)
    {
        var orphans = db.Tables.IgnoreQueryFilters()
            .Where(t => t.RestaurantId == 0)
            .OrderBy(t => t.Id)
            .ToList();
        if (orphans.Count == 0)
            return;

        var used = db.Tables.IgnoreQueryFilters()
            .Where(t => t.RestaurantId == restaurantId)
            .Select(t => t.TableNumber)
            .ToHashSet();

        var next = used.Count == 0 ? 1 : used.Max() + 1;

        foreach (var table in orphans)
        {
            if (table.TableNumber <= 0 || !used.Add(table.TableNumber))
            {
                while (!used.Add(next))
                    next++;
                table.TableNumber = next;
                next++;
            }

            table.RestaurantId = restaurantId;
        }

        db.SaveChanges();
    }
}
