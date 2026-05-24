using System.Net;
using System.Net.Http.Json;
using EliteRestaurant.Contracts.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EliteRestaurant.Tests.Api;

[CollectionDefinition(nameof(SetupApiCollection), DisableParallelization = true)]
public sealed class SetupApiCollection;

[Collection(nameof(SetupApiCollection))]
public class SetupApiTests : IClassFixture<SetupWebApplicationFactory>
{
    private readonly SetupWebApplicationFactory _factory;

    public SetupApiTests(SetupWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PostFirstSite_CreatesRestaurantAndAdmin()
    {
        var client = _factory.CreateClient();
        var statusBefore = await client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status");
        Assert.NotNull(statusBefore);
        Assert.True(statusBefore!.SetupRequired);
        Assert.Equal(0, statusBefore.RestaurantCount);

        var response = await client.PostAsJsonAsync(
            "/api/setup/first-site",
            new SiteSetupRequest(
                "Test Bistro",
                "test-bistro",
                "testbistro.example",
                "testadmin",
                "5678",
                "Test Admin",
                "en"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SiteSetupResponse>();
        Assert.NotNull(body);
        Assert.Equal("test-bistro", body!.Slug);
        Assert.True(body.RestaurantId > 0);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        var status = await client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status");
        Assert.NotNull(status);
        Assert.False(status!.SetupRequired);
    }

    [Fact]
    public async Task PostFirstSite_WhenMigrationPlaceholderWithoutAdmin_CompletesSetup()
    {
        using var factory = new SetupWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EliteRestaurant.Core.Data.AppDbContext>();
        db.Restaurants.Add(new EliteRestaurant.Core.Models.Restaurant
        {
            UniqueId = "REST-DEFAULT-001",
            Name = "Elite Restaurant",
            Slug = "etoile-gourmande",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var statusBefore = await client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status");
        Assert.NotNull(statusBefore);
        Assert.True(statusBefore!.SetupRequired);
        Assert.Equal(1, statusBefore.RestaurantCount);

        var response = await client.PostAsJsonAsync(
            "/api/setup/first-site",
            new SiteSetupRequest(
                "Étoile Gourmande",
                "etoile-gourmande",
                "etoilegourmandekin.com",
                "admin",
                "1234",
                "Admin",
                "en"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostFirstSite_WhenAlreadySetup_Returns409()
    {
        var client = _factory.CreateClient();
        _ = await client.PostAsJsonAsync(
            "/api/setup/first-site",
            new SiteSetupRequest("A", "site-a", null, "admina", "1111", null, null));

        var second = await client.PostAsJsonAsync(
            "/api/setup/first-site",
            new SiteSetupRequest("B", "site-b", null, "adminb", "2222", null, null));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
