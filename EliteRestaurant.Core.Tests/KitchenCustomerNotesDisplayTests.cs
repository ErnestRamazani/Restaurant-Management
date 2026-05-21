using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Xunit;

namespace EliteRestaurant.Core.Tests;

public sealed class KitchenCustomerNotesDisplayTests
{
    private const char Dot = '\u00B7';
    private const string EmDash = "\u2014";

    [Fact]
    public void ForKitchen_OnlinePickup_ShowsOnlyUnlabeledCustomerNote()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.Online,
            OrderSource = "TakeOut",
            CustomerNotes =
                $"Guest: Alex Guest{Dot} Online{Dot} Pickup{Dot} Phone: +243 900 000 001{Dot} Make it more spicy{Dot} Pay: Cash"
        };

        Assert.Equal("Make it more spicy", KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    [Fact]
    public void ForKitchen_OnlineDelivery_InstructionsOnly_ReturnsEmDash()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.Online,
            OrderSource = "Delivery",
            CustomerNotes =
                $"Guest: Jane Doe{Dot} Online{Dot} Delivery{Dot} Phone: +243 800 111 222{Dot} Address: 1201 Indiana Avenue{Dot} Instructions: Apartment 4B, ring twice{Dot} Pay: Cash"
        };

        Assert.Equal(EmDash, KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    [Fact]
    public void ForKitchen_OnlineDelivery_ExcludesInstructions_KeepsOnlyPublicMenuNotes()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.Online,
            OrderSource = "Delivery",
            CustomerNotes =
                $"Guest: Jane Doe{Dot} Online{Dot} Delivery{Dot} Phone: +243 800 111 222{Dot} Address: 1201 Indiana Avenue{Dot} Instructions: Door B{Dot} Extra napkins please{Dot} Pay: Card"
        };

        Assert.Equal("Extra napkins please", KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    /// <summary>
    /// Matches <c>PublicMenuController</c> join order: Guest, Online · channel, Phone, Address (delivery),
    /// Instructions (escaped delivery instructions), raw <c>body.Notes</c>, Pay.
    /// </summary>
    [Fact]
    public void ForKitchen_OnlineDelivery_PublicMenuEncoding_InstructionsAndBodyNotes()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.Online,
            OrderSource = "Delivery",
            CustomerNotes =
                $"Guest: Alex Guest{Dot} Online{Dot} Delivery{Dot} Phone: +243 900 000 001{Dot} Address: 1201 Indiana Ave{Dot} Instructions: Meet me at the backyard{Dot} Make it more spicy{Dot} Pay: Cash"
        };

        Assert.Equal("Make it more spicy", KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    [Fact]
    public void ForKitchen_OnlineGuestBlockOnly_ReturnsEmDash()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.Online,
            OrderSource = "TakeOut",
            CustomerNotes =
                $"Guest: Alex Guest{Dot} Online{Dot} Pickup{Dot} Phone: +243 900 000 001{Dot} Pay: Cash"
        };

        Assert.Equal(EmDash, KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    [Fact]
    public void ForKitchen_InStore_ReturnsRawNotes()
    {
        var order = new OrderRecord
        {
            OrderOrigin = OrderOrigin.InStore,
            CustomerNotes = "  Table by the window  "
        };

        Assert.Equal("Table by the window", KitchenCustomerNotesDisplay.ForKitchen(order));
    }

    [Fact]
    public void ForKitchen_Online_EmptyCustomerNotes_ReturnsEmDash()
    {
        var order = new OrderRecord { OrderOrigin = OrderOrigin.Online, CustomerNotes = "   " };

        Assert.Equal(EmDash, KitchenCustomerNotesDisplay.ForKitchen(order));
    }
}
