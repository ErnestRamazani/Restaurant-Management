using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Core.Data;

/// <summary>Deducts inventory when an order enters the kitchen queue (Waiting).</summary>
public static class OrderInventoryDeduction
{
    /// <summary>Matches user-facing headers used by deduction paths.</summary>
    public enum InventoryValidationKind
    {
        FullOrder,
        AdditionalLinesOnly
    }

    /// <summary>
    /// Read-only stock check using the same requirements math as <see cref="TryApplyForPlacedOrder"/> /
    /// <see cref="TryApplyForAdditionalItems"/>. Does not modify the database.
    /// </summary>
    public static string? TryValidateInventoryForProductQuantities(
        AppDbContext db,
        IReadOnlyList<(int ProductId, int Quantity)> selectedLines,
        InventoryValidationKind kind = InventoryValidationKind.FullOrder)
    {
        if (selectedLines.Count == 0)
            return "Order has no line items.";

        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();

        var productIds = selectedLines.Select(s => s.ProductId).Distinct().ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        var ingredientRows = db.ProductIngredients
            .AsNoTracking()
            .Include(pi => pi.InventoryItem)
            .Where(pi => productIds.Contains(pi.ProductId))
            .ToList();

        var requiredByInventory = new Dictionary<int, decimal>();
        var requiredByInventoryAndAssignee =
            new Dictionary<(int InventoryItemId, int? EmployeeId, string Role, string Name), decimal>();
        foreach (var line in selectedLines)
        {
            var assignee = ResolvePreparationAssignee(line.ProductId);
            foreach (var ingredient in ingredientRows.Where(i => i.ProductId == line.ProductId))
            {
                var required = ingredient.Quantity * line.Quantity;
                if (!requiredByInventory.TryAdd(ingredient.InventoryItemId, required))
                    requiredByInventory[ingredient.InventoryItemId] += required;

                var actorKey = (ingredient.InventoryItemId, assignee.EmployeeId, assignee.Role, assignee.Name);
                if (!requiredByInventoryAndAssignee.TryAdd(actorKey, required))
                    requiredByInventoryAndAssignee[actorKey] += required;
            }
        }

        var insufficient = ingredientRows
            .Where(i => i.InventoryItem != null && requiredByInventory.TryGetValue(i.InventoryItemId, out var req) &&
                        i.InventoryItem.StockQuantity < req)
            .Select(i =>
                $"{i.InventoryItem!.Name} (need {requiredByInventory[i.InventoryItemId]:0.##} {i.InventoryItem.Unit}, have {i.InventoryItem.StockQuantity:0.##})")
            .Distinct()
            .ToList();

        if (insufficient.Count == 0)
            return null;

        var header = kind == InventoryValidationKind.AdditionalLinesOnly
            ? "Not enough inventory for these add-on items:\n\n"
            : "Not enough inventory for this order:\n\n";
        return header + string.Join("\n", insufficient);
    }

    /// <summary>
    /// Caller must have opened <see cref="DatabaseFacade.BeginTransaction"/> on <paramref name="db"/>
    /// so stock updates commit with the same unit of work as order rows and table reconciliation.
    /// </summary>
    public static string? TryApplyForPlacedOrder(AppDbContext db, OrderRecord order)
    {
        EnsureAmbientTransaction(db);

        var selectedLines = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        if (selectedLines.Count == 0)
            return "Order has no line items.";

        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();

        var productIds = selectedLines.Select(s => s.ProductId).Distinct().ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        var ingredientRows = db.ProductIngredients
            .Include(pi => pi.InventoryItem)
            .Where(pi => productIds.Contains(pi.ProductId))
            .ToList();

        var requiredByInventory = new Dictionary<int, decimal>();
        var requiredByInventoryAndAssignee =
            new Dictionary<(int InventoryItemId, int? EmployeeId, string Role, string Name), decimal>();
        foreach (var line in selectedLines)
        {
            var assignee = ResolvePreparationAssignee(line.ProductId);
            foreach (var ingredient in ingredientRows.Where(i => i.ProductId == line.ProductId))
            {
                var required = ingredient.Quantity * line.Quantity;
                if (!requiredByInventory.TryAdd(ingredient.InventoryItemId, required))
                    requiredByInventory[ingredient.InventoryItemId] += required;

                var actorKey = (ingredient.InventoryItemId, assignee.EmployeeId, assignee.Role, assignee.Name);
                if (!requiredByInventoryAndAssignee.TryAdd(actorKey, required))
                    requiredByInventoryAndAssignee[actorKey] += required;
            }
        }

        var insufficient = ingredientRows
            .Where(i => i.InventoryItem != null && requiredByInventory.TryGetValue(i.InventoryItemId, out var req) &&
                        i.InventoryItem.StockQuantity < req)
            .Select(i =>
                $"{i.InventoryItem!.Name} (need {requiredByInventory[i.InventoryItemId]:0.##} {i.InventoryItem.Unit}, have {i.InventoryItem.StockQuantity:0.##})")
            .Distinct()
            .ToList();

        if (insufficient.Count > 0)
            return "Not enough inventory for this order:\n\n" + string.Join("\n", insufficient);

        foreach (var (inventoryItemId, required) in requiredByInventory)
        {
            var inventoryItem = ingredientRows.Select(i => i.InventoryItem).FirstOrDefault(i => i?.Id == inventoryItemId);
            if (inventoryItem is null)
                continue;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z - {required:0.##} {inventoryItem.Name} deducted from order {order.UniqueId}. Used by {actorText}.";

            var rows = ApplyAtomicStockDecrement(db, inventoryItemId, required, deductionNote);
            if (rows != 1)
            {
                var name = db.InventoryItems.AsNoTracking()
                    .Where(i => i.Id == inventoryItemId)
                    .Select(i => i.Name)
                    .FirstOrDefault() ?? "inventory item";
                return
                    "Not enough inventory to complete this deduction (stock may have changed). Please refresh and try again:\n\n" +
                    name;
            }
        }

        return null;
    }

    /// <summary>
    /// Cloud/desktop sync path: validates stock and mutates <paramref name="inventoryById"/> (quantity + notes). Does not use EF.
    /// </summary>
    public static string? TryApplyForPlacedOrderMemory(
        OrderRecord order,
        Dictionary<int, InventoryItem> inventoryById,
        IReadOnlyList<ProductIngredient> ingredientRows,
        IReadOnlyList<Employee> activeStaff,
        IReadOnlyDictionary<int, Product> productById)
    {
        var selectedLines = order.Items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        if (selectedLines.Count == 0)
            return "Order has no line items.";

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        var requiredByInventory = new Dictionary<int, decimal>();
        var requiredByInventoryAndAssignee =
            new Dictionary<(int InventoryItemId, int? EmployeeId, string Role, string Name), decimal>();
        foreach (var line in selectedLines)
        {
            var assignee = ResolvePreparationAssignee(line.ProductId);
            foreach (var ingredient in ingredientRows.Where(i => i.ProductId == line.ProductId))
            {
                var required = ingredient.Quantity * line.Quantity;
                if (!requiredByInventory.TryAdd(ingredient.InventoryItemId, required))
                    requiredByInventory[ingredient.InventoryItemId] += required;

                var actorKey = (ingredient.InventoryItemId, assignee.EmployeeId, assignee.Role, assignee.Name);
                if (!requiredByInventoryAndAssignee.TryAdd(actorKey, required))
                    requiredByInventoryAndAssignee[actorKey] += required;
            }
        }

        var insufficient = new List<string>();
        foreach (var (inventoryItemId, req) in requiredByInventory)
        {
            if (!inventoryById.TryGetValue(inventoryItemId, out var inv))
                continue;
            if (inv.StockQuantity < req)
                insufficient.Add(
                    $"{inv.Name} (need {req:0.##} {inv.Unit}, have {inv.StockQuantity:0.##})");
        }

        if (insufficient.Count > 0)
            return "Not enough inventory for this order:\n\n" + string.Join("\n", insufficient.Distinct());

        foreach (var (inventoryItemId, required) in requiredByInventory)
        {
            if (!inventoryById.TryGetValue(inventoryItemId, out var inventoryItem))
                continue;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z - {required:0.##} {inventoryItem.Name} deducted from order {order.UniqueId}. Used by {actorText}.";

            if (inventoryItem.StockQuantity < required)
            {
                return
                    "Not enough inventory to complete this deduction (stock may have changed). Please refresh and try again:\n\n" +
                    inventoryItem.Name;
            }

            inventoryItem.StockQuantity -= required;
            inventoryItem.Notes = string.IsNullOrWhiteSpace(inventoryItem.Notes)
                ? deductionNote
                : $"{inventoryItem.Notes.Trim()}\n{deductionNote}";
        }

        return null;
    }

    /// <summary>
    /// Caller must have opened <see cref="DatabaseFacade.BeginTransaction"/> on <paramref name="db"/>.
    /// </summary>
    public static string? TryApplyForAdditionalItems(AppDbContext db, OrderRecord order, IReadOnlyList<OrderItem> additionalItems)
    {
        EnsureAmbientTransaction(db);

        if (additionalItems.Count == 0)
            return null;

        var selectedLines = additionalItems
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(i => i.Quantity)))
            .ToList();

        var activeStaff = db.Employees
            .AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .ToList();

        var productIds = selectedLines.Select(s => s.ProductId).Distinct().ToList();
        var productById = db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p);

        (int? EmployeeId, string Role, string Name) ResolvePreparationAssignee(int productId)
        {
            if (!productById.TryGetValue(productId, out var product))
                return (null, "Unknown", "Unassigned");

            var isDrink = string.Equals(product.Category, "Drink", StringComparison.OrdinalIgnoreCase);
            if (isDrink)
            {
                var barman = activeStaff.FirstOrDefault(e =>
                    e.Role.Equals("Barman", StringComparison.OrdinalIgnoreCase) ||
                    e.Role.Equals("Bartender", StringComparison.OrdinalIgnoreCase));
                return barman is null ? (null, "Barman", "Unassigned Barman") : (barman.Id, "Barman", barman.Name);
            }

            var chef = activeStaff.FirstOrDefault(e =>
                e.Role.Equals("Chef", StringComparison.OrdinalIgnoreCase));
            return chef is null ? (null, "Chef", "Unassigned Chef") : (chef.Id, "Chef", chef.Name);
        }

        var ingredientRows = db.ProductIngredients
            .Include(pi => pi.InventoryItem)
            .Where(pi => productIds.Contains(pi.ProductId))
            .ToList();

        var requiredByInventory = new Dictionary<int, decimal>();
        var requiredByInventoryAndAssignee =
            new Dictionary<(int InventoryItemId, int? EmployeeId, string Role, string Name), decimal>();
        foreach (var line in selectedLines)
        {
            var assignee = ResolvePreparationAssignee(line.ProductId);
            foreach (var ingredient in ingredientRows.Where(i => i.ProductId == line.ProductId))
            {
                var required = ingredient.Quantity * line.Quantity;
                if (!requiredByInventory.TryAdd(ingredient.InventoryItemId, required))
                    requiredByInventory[ingredient.InventoryItemId] += required;

                var actorKey = (ingredient.InventoryItemId, assignee.EmployeeId, assignee.Role, assignee.Name);
                if (!requiredByInventoryAndAssignee.TryAdd(actorKey, required))
                    requiredByInventoryAndAssignee[actorKey] += required;
            }
        }

        var insufficient = ingredientRows
            .Where(i => i.InventoryItem != null && requiredByInventory.TryGetValue(i.InventoryItemId, out var req) &&
                        i.InventoryItem.StockQuantity < req)
            .Select(i =>
                $"{i.InventoryItem!.Name} (need {requiredByInventory[i.InventoryItemId]:0.##} {i.InventoryItem.Unit}, have {i.InventoryItem.StockQuantity:0.##})")
            .Distinct()
            .ToList();

        if (insufficient.Count > 0)
            return "Not enough inventory for these add-on items:\n\n" + string.Join("\n", insufficient);

        foreach (var (inventoryItemId, required) in requiredByInventory)
        {
            var inventoryItem = ingredientRows.Select(i => i.InventoryItem).FirstOrDefault(i => i?.Id == inventoryItemId);
            if (inventoryItem is null)
                continue;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z - {required:0.##} {inventoryItem.Name} deducted (add-on) from order {order.UniqueId}. Used by {actorText}.";

            var rows = ApplyAtomicStockDecrement(db, inventoryItemId, required, deductionNote);
            if (rows != 1)
            {
                var name = db.InventoryItems.AsNoTracking()
                    .Where(i => i.Id == inventoryItemId)
                    .Select(i => i.Name)
                    .FirstOrDefault() ?? "inventory item";
                return
                    "Not enough inventory for these add-on items (stock may have changed). Please refresh and try again:\n\n" +
                    name;
            }
        }

        return null;
    }

    private static void EnsureAmbientTransaction(AppDbContext db)
    {
        if (IsInMemoryProvider(db))
            return;
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Order inventory deduction must run inside Database.BeginTransaction() so stock changes commit with the order.");
        }
    }

    private static bool IsInMemoryProvider(AppDbContext db) =>
        db.Database.ProviderName is { } p &&
        p.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

    /// <summary>Single atomic decrement; returns rows affected (1 on success, 0 if insufficient stock or missing row).</summary>
    private static int ApplyAtomicStockDecrement(AppDbContext db, int inventoryItemId, decimal required, string deductionNote)
    {
        return db.Database.ExecuteSqlRaw(
            """
            UPDATE "InventoryItems"
            SET "StockQuantity" = "StockQuantity" - {0},
                "Notes" = CASE
                    WHEN COALESCE(TRIM("Notes"), '') = '' THEN {1}
                    ELSE "Notes" || chr(10) || {1}
                END
            WHERE "Id" = {2} AND "StockQuantity" >= {0}
            """,
            required, deductionNote, inventoryItemId);
    }
}
