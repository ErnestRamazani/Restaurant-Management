using EliteRestaurantPro.Models;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.Data;

/// <summary>Deducts inventory when an order enters the kitchen queue (Waiting).</summary>
public static class OrderInventoryDeduction
{
    /// <summary>Returns null if deduction succeeded; otherwise a user-facing error message.</summary>
    public static string? TryApplyForPlacedOrder(AppDbContext db, OrderRecord order)
    {
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
            var inventoryItem = db.InventoryItems.FirstOrDefault(i => i.Id == inventoryItemId);
            if (inventoryItem is null)
                continue;

            inventoryItem.StockQuantity -= required;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.Now:yyyy-MM-dd HH:mm} - {required:0.##} {inventoryItem.Name} deducted from order {order.UniqueId}. Used by {actorText}.";
            inventoryItem.Notes = string.IsNullOrWhiteSpace(inventoryItem.Notes)
                ? deductionNote
                : $"{inventoryItem.Notes}\n{deductionNote}";
        }

        return null;
    }

    /// <summary>Deducts inventory only for newly added lines (append to an existing order).</summary>
    public static string? TryApplyForAdditionalItems(AppDbContext db, OrderRecord order, IReadOnlyList<OrderItem> additionalItems)
    {
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
            var inventoryItem = db.InventoryItems.FirstOrDefault(i => i.Id == inventoryItemId);
            if (inventoryItem is null)
                continue;

            inventoryItem.StockQuantity -= required;

            var actorNotes = requiredByInventoryAndAssignee
                .Where(x => x.Key.InventoryItemId == inventoryItemId)
                .Select(x => $"{x.Key.Role} {x.Key.Name}: {x.Value:0.##}")
                .ToList();
            var actorText = actorNotes.Count == 0 ? "Unassigned" : string.Join(", ", actorNotes);
            var deductionNote =
                $"{DateTime.Now:yyyy-MM-dd HH:mm} - {required:0.##} {inventoryItem.Name} deducted (add-on) from order {order.UniqueId}. Used by {actorText}.";
            inventoryItem.Notes = string.IsNullOrWhiteSpace(inventoryItem.Notes)
                ? deductionNote
                : $"{inventoryItem.Notes}\n{deductionNote}";
        }

        return null;
    }
}
