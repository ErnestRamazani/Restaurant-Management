using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
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

public class StaffOrdersCancelAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StaffOrdersCancelAuthorizationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));

    [Fact]
    public async Task StaffCancel_WithServerToken_AllowsRequest()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "srvfloor", "1111", "Server");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/staff/orders/99999/cancel", new { passcode = "x" });
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaffCancel_WithServerToken_CancelsWaitingOrder()
    {
        int orderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = db.Products.FirstOrDefault()
                ?? throw new InvalidOperationException("Integration seed must provide at least one product.");
            var order = new OrderRecord
            {
                UniqueId = $"INT-CANCEL-{Guid.NewGuid():N}",
                Status = "Waiting",
                OrderOrigin = OrderOrigin.InStore,
                PaymentCurrencyCode = CurrencyHelper.Usd,
                CreatedAt = DateTime.UtcNow
            };
            order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 1 });
            db.Orders.Add(order);
            db.SaveChanges();
            orderId = order.Id;
        }

        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "srvfloor", "1111", "Server");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            $"/api/staff/orders/{orderId}/cancel",
            new { passcode = "cancelgate" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Cancelled", db.Orders.AsNoTracking().Single(o => o.Id == orderId).Status);
        }
    }

    [Fact]
    public async Task StaffCancel_WithChefToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "chefint", "9999", "KitchenBar");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/staff/orders/1/cancel", new { passcode = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string staffId, string pin, string portal)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { staffId, pin, portal });
        login.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await login.Content.ReadAsStringAsync());
        var token = node?["accessToken"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
