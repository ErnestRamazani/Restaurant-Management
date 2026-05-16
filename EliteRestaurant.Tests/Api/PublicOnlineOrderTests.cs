using System.Net;
using System.Net.Http.Json;
using EliteRestaurant.Api;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public class PublicOnlineOrderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicOnlineOrderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    private static Tuple<int, int> ResolveProductIds(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var food = db.Products.First(p => p.UniqueId == "P-INT-FOOD");
        var drink = db.Products.First(p => p.UniqueId == "P-INT-DRINK");
        return Tuple.Create(food.Id, drink.Id);
    }

    [Fact]
    public async Task PostOnlineOrder_Pickup_MixedCart_DeferredPayment_TakeOut()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "Alex Guest",
            customerPhone = "+243 900 000 001",
            fulfillmentMode = "Pickup",
            paymentMethod = "Card",
            items = new[]
            {
                new { productId = ids.Item1, quantity = 1, unitPrice = 10m },
                new { productId = ids.Item2, quantity = 1, unitPrice = 3m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<SubmitOnlineJson>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.orderCode));
        Assert.False(string.IsNullOrWhiteSpace(payload.confirmationCode));
        Assert.Matches(@"^\d{6}$", payload.confirmationCode!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = db.Orders.Include(o => o.Items).First(o => o.UniqueId == payload.orderCode);
        Assert.Equal(payload.confirmationCode, order.ConfirmationCode);
        Assert.Equal(OrderWorkflow.PendingApproval, order.Status);
        Assert.Equal("TakeOut", order.OrderSource);
        Assert.Equal(OrderOrigin.Online, order.OrderOrigin);
        Assert.Equal(0m, order.DeliveryFeeUsd);
        Assert.Equal(OrderPaymentTiming.Deferred, order.PaymentTiming);
        Assert.Equal("Card", order.GuestPaymentMethod);
        Assert.Equal(2, order.Items.Count);
        Assert.Contains("Phone: +243 900 000 001", order.CustomerNotes ?? string.Empty);
        Assert.Equal("Alex Guest", order.ReservationGuestName);
    }

    [Fact]
    public async Task PostOnlineOrder_Pickup_WithoutPhone_ReturnsBadRequest()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "Alex Guest",
            fulfillmentMode = "Pickup",
            paymentMethod = "Card",
            items = new[] { new { productId = ids.Item1, quantity = 1, unitPrice = 10m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostOnlineOrder_Delivery_SetsDeliveryFee_AndAddressNote()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "Delivery Pat",
            fulfillmentMode = "Delivery",
            customerPhone = "+243 825 234 613",
            deliveryAddress = "123 Kinshasa Ave, Suite 400 — test lane",
            deliveryInstructions = "Ring bell",
            paymentMethod = "MobileMoney",
            items = new[]
            {
                new { productId = ids.Item1, quantity = 1, unitPrice = 10m },
                new { productId = ids.Item2, quantity = 1, unitPrice = 3m }
            }
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<SubmitOnlineJson>();
        Assert.NotNull(payload);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = db.Orders.First(o => o.UniqueId == payload.orderCode);
        Assert.Equal("Delivery", order.OrderSource);
        Assert.Equal(2.60m, order.DeliveryFeeUsd);
        Assert.Contains("123 Kinshasa", order.CustomerNotes, StringComparison.Ordinal);
        Assert.Contains("Ring bell", order.CustomerNotes, StringComparison.Ordinal);
        Assert.Contains("+243 825", order.CustomerNotes, StringComparison.Ordinal);
        Assert.Equal("Delivery Pat", order.ReservationGuestName);
        Assert.Equal("MobileMoney", order.GuestPaymentMethod);
    }

    [Fact]
    public async Task PostOnlineOrder_Delivery_WithoutPhone_Returns400()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "No Phone",
            fulfillmentMode = "Delivery",
            deliveryAddress = "123 Kinshasa Ave",
            items = new[] { new { productId = ids.Item1, quantity = 1, unitPrice = 10m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostOnlineOrder_Delivery_PreservesMultilineAddress_InNotes()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);
        const string multilineAddress = "123 Kinshasa Ave\nBuilding B, Floor 3";

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "Multi Line",
            fulfillmentMode = "Delivery",
            customerPhone = "+243 825 111 222",
            deliveryAddress = multilineAddress,
            paymentMethod = "Cash",
            items = new[] { new { productId = ids.Item1, quantity = 1, unitPrice = 10m } }
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var payload = await res.Content.ReadFromJsonAsync<SubmitOnlineJson>();
        Assert.NotNull(payload);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = db.Orders.First(o => o.UniqueId == payload.orderCode);
        Assert.Contains("123 Kinshasa Ave\\nBuilding B, Floor 3", order.CustomerNotes, StringComparison.Ordinal);

        var ticket = DeliveryTicketInfoParser.TryParse(order);
        Assert.NotNull(ticket);
        Assert.Equal(multilineAddress, ticket.Address);
    }

    [Fact]
    public async Task PostOnlineOrder_Delivery_WithoutAddress_Returns400()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();
        var ids = ResolveProductIds(factory.Services);

        var res = await client.PostAsJsonAsync("/api/public/menu/orders/online", new
        {
            customerName = "No Addr",
            fulfillmentMode = "Delivery",
            items = new[] { new { productId = ids.Item1, quantity = 1, unitPrice = 10m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

#pragma warning disable CS0649
    private sealed class SubmitOnlineJson
    {
        public string orderCode { get; set; } = string.Empty;
        public string? confirmationCode { get; set; }
        public int orderId { get; set; }
    }
#pragma warning restore CS0649
}
