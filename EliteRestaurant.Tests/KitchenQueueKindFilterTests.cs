using EliteRestaurant.Core.Models;

using EliteRestaurant.Core.Orders;

using Xunit;



namespace EliteRestaurant.Tests;



public class KitchenQueueKindFilterTests

{

    private static OrderRecord OrderWithCategories(params string[] categories)

    {

        var items = categories.Select((c, i) => new OrderItem

        {

            Id = i + 1,

            ProductId = i + 1,

            Quantity = 1,

            Product = new Product { Id = i + 1, Category = c, Name = c, Price = 1m }

        }).ToList();

        return new OrderRecord { Id = 1, UniqueId = "T-1", Status = "Waiting", Items = items };

    }



    [Fact]

    public void TryInferOrderCheckKind_food_only()

    {

        var kind = KitchenQueueKindFilter.TryInferOrderCheckKind(OrderWithCategories("Main", "Appetizer"));

        Assert.Equal(OpenCheckKindHelper.Food, kind);

    }



    [Fact]

    public void TryInferOrderCheckKind_drink_only()

    {

        var kind = KitchenQueueKindFilter.TryInferOrderCheckKind(OrderWithCategories("Drink", "Bar"));

        Assert.Equal(OpenCheckKindHelper.Drink, kind);

    }



    [Fact]

    public void TryInferOrderCheckKind_mixed_returns_null()

    {

        var kind = KitchenQueueKindFilter.TryInferOrderCheckKind(OrderWithCategories("Drink", "Main"));

        Assert.Null(kind);

    }



    [Theory]

    [InlineData("Kitchen", "Main", true)]

    [InlineData("Kitchen", "Drink", false)]

    [InlineData("Bar", "Drink", true)]

    [InlineData("Bar", "Main", false)]

    [InlineData("KitchenBar", "Drink", true)]

    [InlineData("KitchenBar", "Main", true)]

    public void OrderMatchesPortalQueue_splits_by_portal(string portal, string category, bool expected)

    {

        var order = OrderWithCategories(category);

        Assert.Equal(expected, KitchenQueueKindFilter.OrderMatchesPortalQueue(portal, order));

    }



    [Fact]

    public void OrderMatchesPortalQueue_mixed_appears_on_kitchen_and_bar()

    {

        var mixed = OrderWithCategories("Drink", "Main");

        Assert.True(KitchenQueueKindFilter.OrderMatchesPortalQueue("Kitchen", mixed));

        Assert.True(KitchenQueueKindFilter.OrderMatchesPortalQueue("Bar", mixed));

    }



    [Fact]

    public void FilterForPortal_kitchen_excludes_drinks()

    {

        var food = OrderWithCategories("Main");

        var drink = OrderWithCategories("Drink");

        var list = KitchenQueueKindFilter.FilterForPortal("Kitchen", new[] { food, drink }).ToList();

        Assert.Single(list);

        Assert.Equal(food.Id, list[0].Id);

    }



    [Fact]

    public void FilterForPortal_mixed_included_on_both_portals()

    {

        var mixed = OrderWithCategories("Drink", "Main");

        var kitchen = KitchenQueueKindFilter.FilterForPortal("Kitchen", new[] { mixed }).ToList();

        var bar = KitchenQueueKindFilter.FilterForPortal("Bar", new[] { mixed }).ToList();

        Assert.Single(kitchen);

        Assert.Single(bar);

    }



    [Fact]

    public void FilterItemsForPortal_kitchen_returns_food_lines_only()

    {

        var order = OrderWithCategories("Drink", "Main", "Appetizer");

        var items = KitchenQueueKindFilter.FilterItemsForPortal("Kitchen", order.Items!.ToList());

        Assert.Equal(2, items.Count);

        Assert.All(items, i => Assert.False(OpenCheckKindHelper.IsDrinkCategory(i.Product!.Category)));

    }



    [Fact]

    public void FilterItemsForPortal_bar_returns_drink_lines_only()

    {

        var order = OrderWithCategories("Drink", "Main");

        var items = KitchenQueueKindFilter.FilterItemsForPortal("Bar", order.Items!.ToList());

        Assert.Single(items);

        Assert.True(OpenCheckKindHelper.IsDrinkCategory(items[0].Product!.Category));

    }



    [Fact]

    public void ToQueueRow_kitchen_portal_summarizes_food_lines_only()

    {

        var prepared = DateTime.UtcNow.AddHours(-1);

        var order = OrderWithCategories("Drink", "Main", "Dessert");

        order.Items!.ElementAt(1).KitchenPreparedAt = prepared;

        var row = KitchenOrderQueueMapper.ToQueueRow(order, "Kitchen");

        Assert.Equal(2, row.Items.Count);

        Assert.Equal(1, row.KitchenNewLineCount);

        Assert.DoesNotContain(row.Items, i => i.ProductId == 1);

    }



    [Fact]

    public void FilterForPortal_does_not_use_inferred_check_kind_for_inclusion()

    {

        var mixed = OrderWithCategories("Drink", "Main");

        Assert.Null(KitchenQueueKindFilter.TryInferOrderCheckKind(mixed));

        Assert.Contains(mixed, KitchenQueueKindFilter.FilterForPortal("Kitchen", [mixed]));

        Assert.Contains(mixed, KitchenQueueKindFilter.FilterForPortal("Bar", [mixed]));

    }



    [Fact]

    public void OrderMatchesPortalQueue_requires_loaded_products_for_classification()

    {

        var order = new OrderRecord

        {

            Id = 2,

            UniqueId = "T-2",

            Status = "Waiting",

            Items =

            [

                new OrderItem { Id = 1, ProductId = 10, Quantity = 1, Product = null },

                new OrderItem { Id = 2, ProductId = 11, Quantity = 1, Product = null }

            ]

        };

        Assert.False(KitchenQueueKindFilter.OrderMatchesPortalQueue("Kitchen", order));

        Assert.False(KitchenQueueKindFilter.OrderMatchesPortalQueue("Bar", order));

    }



    [Fact]

    public void ToQueueRow_mixed_order_bar_portal_returns_drink_lines_only()

    {

        var order = OrderWithCategories("Drink", "Main");

        var row = KitchenOrderQueueMapper.ToQueueRow(order, "Bar");

        Assert.Single(row.Items);

        Assert.Equal(OpenCheckKindHelper.Food, row.CheckKind);

    }

}


