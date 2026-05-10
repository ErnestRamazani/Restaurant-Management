using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using EliteRestaurant.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public class FloorReservationAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FloorReservationAuthorizationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));

    [Fact]
    public async Task FloorSnapshot_WithServerRoleToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "srvfloor", "1111", "Server");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/floor/snapshot");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FloorSnapshot_WithCashierRoleToken_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "cashfloor", "2222", "Cashier");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/floor/snapshot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthSession_WithCashierToken_IncludesRole()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "cashfloor", "2222", "Cashier");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/session");
        response.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Cashier", node?["role"]?.GetValue<string>());
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
