using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EliteRestaurant.Api;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public sealed class AdminSyncRestaurantIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminSyncRestaurantIdTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));

    [Fact]
    public async Task Sync_Upsert_DoesNotClearRestaurantId_OnExistingEmployee()
    {
        int employeeId;
        int restaurantIdBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var restaurant = db.Restaurants.IgnoreQueryFilters().OrderBy(r => r.Id).FirstOrDefault();
            if (restaurant is null)
            {
                restaurant = new Restaurant
                {
                    UniqueId = RestaurantTenantBootstrap.DefaultUniqueId,
                    Slug = RestaurantTenantBootstrap.DefaultSlug,
                    Name = "Integration Test Restaurant",
                    CustomDomain = RestaurantTenantBootstrap.DefaultDomain
                };
                db.Restaurants.Add(restaurant);
                db.SaveChanges();
            }

            RestaurantTenantBootstrap.EnsureDefaultRestaurant(db);

            var chef = db.Employees.IgnoreQueryFilters().First(e => e.SignInId == "chefint");
            employeeId = chef.Id;
            restaurantIdBefore = chef.RestaurantId > 0 ? chef.RestaurantId : restaurant.Id;
            if (chef.RestaurantId != restaurantIdBefore)
            {
                chef.RestaurantId = restaurantIdBefore;
                db.SaveChanges();
            }

            Assert.True(restaurantIdBefore > 0);
        }

        var client = _factory.CreateClient();
        var token = await LoginChefAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = JsonSerializer.SerializeToElement(new
        {
            id = employeeId,
            restaurantId = 0,
            uniqueId = "EMP-CHEF-INTTEST",
            signInId = "chefint",
            name = "Integration Chef (edited)",
            role = "Chef",
            pinCode = "unused",
            employmentStatus = "Active",
            joinDate = DateTime.Today.ToString("yyyy-MM-dd")
        });

        var batch = new
        {
            operations = new[]
            {
                new
                {
                    idempotencyKey = Guid.NewGuid().ToString("N"),
                    entityName = nameof(Employee),
                    operation = "Upsert",
                    payload,
                    queuedAtUtc = DateTime.UtcNow
                }
            }
        };

        var syncResponse = await client.PostAsJsonAsync("/api/admin/sync", batch);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = db.Employees.First(e => e.Id == employeeId);
            Assert.Equal(restaurantIdBefore, updated.RestaurantId);
            Assert.Equal("Integration Chef (edited)", updated.Name);
            Assert.Contains(db.Employees, e => e.Id == employeeId);
        }
    }

    private static async Task<string> LoginChefAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { staffId = "chefint", pin = "9999", portal = "KitchenBar" });
        login.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await login.Content.ReadAsStringAsync());
        var token = node?["accessToken"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }
}
