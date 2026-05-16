using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class DeliveryTicketInfoParserTests
{
    [Fact]
    public void TryParse_ReadsGuestPhoneAddressAndInstructions_FromMiddotNotes()
    {
        var order = new OrderRecord
        {
            OrderSource = "Delivery",
            OrderOrigin = OrderOrigin.Online,
            ReservationGuestName = "Jane Doe",
            CustomerNotes =
                "Guest: Jane Doe · Online · Delivery · Phone: +243 800 111 222 · Address: 1201 Indiana Avenue · Instructions: Apartment 4B, ring twice · Pay: Cash"
        };

        var info = DeliveryTicketInfoParser.TryParse(order);

        Assert.NotNull(info);
        Assert.Equal("Jane Doe", info.CustomerName);
        Assert.Equal("+243 800 111 222", info.Phone);
        Assert.Equal("1201 Indiana Avenue", info.Address);
        Assert.Equal("Apartment 4B, ring twice", info.Instructions);
    }

    [Fact]
    public void TryParse_ReadsMultilineAddress_WhenEscapedInNotes()
    {
        var order = new OrderRecord
        {
            OrderSource = "Delivery",
            OrderOrigin = OrderOrigin.Online,
            ReservationGuestName = "Jane Doe",
            CustomerNotes =
                "Guest: Jane Doe · Online · Delivery · Phone: +243 800 111 222 · Address: 1201 Indiana Avenue\\nSuite 400 · Instructions: Ring twice · Pay: Cash"
        };

        var info = DeliveryTicketInfoParser.TryParse(order);

        Assert.NotNull(info);
        Assert.Equal("1201 Indiana Avenue\nSuite 400", info.Address);
    }

    [Fact]
    public void TryParse_ReadsPhone_ForOnlinePickup()
    {
        var order = new OrderRecord
        {
            OrderSource = "TakeOut",
            OrderOrigin = OrderOrigin.Online,
            ReservationGuestName = "Alex Guest",
            CustomerNotes = "Guest: Alex Guest · Online · Pickup · Phone: +243 900 000 001"
        };

        var info = DeliveryTicketInfoParser.TryParse(order);

        Assert.NotNull(info);
        Assert.Equal("Alex Guest", info.CustomerName);
        Assert.Equal("+243 900 000 001", info.Phone);
        Assert.True(string.IsNullOrWhiteSpace(info.Address));
    }
}
