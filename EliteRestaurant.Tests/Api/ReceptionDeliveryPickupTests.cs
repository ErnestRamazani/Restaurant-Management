using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using EliteRestaurant.Api;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public class ReceptionDeliveryPickupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ReceptionDeliveryPickupTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));

    [Fact]
    public async Task DeliveryPickupOrders_WithFrontDeskReceptionLogin_Returns200()
    {
        var client = _factory.CreateClient();
        await SeedOnlinePickupOrderAsync(_factory.Services);

        var token = await LoginAsync(client, "recint", "5101", "Reception");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reception/delivery-pickup-orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServerConfig_WithFrontDeskReceptionLogin_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "recint", "5101", "Reception");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/server/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TablesMy_WithFrontDeskReceptionLogin_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "recint", "5101", "Reception");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/tables/my");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = JsonNode.Parse(body)?.AsArray();
        Assert.NotNull(rows);
        Assert.True(rows.Count > 0, body);
    }

    [Fact]
    public async Task ReceptionTables_WithFrontDeskReceptionLogin_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "recint", "5101", "Reception");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reception/tables");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = JsonNode.Parse(body)?.AsArray();
        Assert.NotNull(rows);
        Assert.True(rows.Count > 0, body);
    }

    [Fact]
    public async Task DeliveryPickupOrders_WithCashierReceptionLogin_Returns200_IncludesTakeOutPickup()
    {
        var client = _factory.CreateClient();
        await SeedOnlinePickupOrderAsync(_factory.Services);

        var token = await LoginAsync(client, "cashfloor", "2222", "Reception");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/reception/delivery-pickup-orders");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = JsonNode.Parse(body)?.AsArray();
        Assert.NotNull(rows);
        Assert.True(rows.Count > 0, body);
        Assert.Contains(rows, r =>
            string.Equals(r?["fulfillmentType"]?.GetValue<string>(), "Pickup", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SeedOnlinePickupOrderAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Orders.Any(o => o.UniqueId == "ORD-RECEPTION-INT"))
            return;

        var table = db.Tables.First(t => t.TableNumber == 99);
        var product = db.Products.First(p => p.UniqueId == "P-INT-FOOD");
        var order = new OrderRecord
        {
            UniqueId = "ORD-RECEPTION-INT",
            TableId = table.Id,
            TableCode = "Table 99",
            Status = OrderWorkflow.PendingApproval,
            OrderSource = "TakeOut",
            OrderOrigin = OrderOrigin.Online,
            ReservationGuestName = "Reception Guest",
            CustomerNotes = "Guest: Reception Guest · Online · Pickup · Phone: +1 555 0100",
            CreatedAt = DateTime.UtcNow,
            Items =
            {
                new OrderItem { ProductId = product.Id, Quantity = 1 }
            }
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
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
