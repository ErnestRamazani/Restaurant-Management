using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using EliteRestaurant.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public class AdminWebAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminWebAuthorizationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));

    [Fact]
    public async Task AdminDashboard_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_WithAdminWebToken_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, IntegrationTestSeed.AdminWebTestSignInId, IntegrationTestSeed.AdminWebTestPin, "AdminWeb");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminSync_WithAdminWebToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, IntegrationTestSeed.AdminWebTestSignInId, IntegrationTestSeed.AdminWebTestPin, "AdminWeb");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/admin/sync", new { operations = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminSync_WithChefToken_Returns200_EmptyBatch()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "chefint", "9999", "KitchenBar");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/admin/sync", new { operations = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
