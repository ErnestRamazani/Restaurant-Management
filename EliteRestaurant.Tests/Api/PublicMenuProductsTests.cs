using System.Net;
using System.Net.Http.Json;
using EliteRestaurant.Api;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public class PublicMenuProductsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicMenuProductsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task GetProducts_ReturnsUpdatedDescriptionAndComposition_FromDatabase()
    {
        var factory = CreateFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = db.Products.First(p => p.UniqueId == "P-INT-FOOD");
            product.Description = "Updated from desktop sync test";
            product.Composition = "ginger, garlic, onion";
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/public/menu/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var payload = await response.Content.ReadFromJsonAsync<List<PublicProductJson>>();
        var row = payload!.First(p => p.uniqueId == "P-INT-FOOD");
        Assert.Equal("Updated from desktop sync test", row.description);
        Assert.Equal("ginger, garlic, onion", row.composition);
    }

#pragma warning disable CS0649
    private sealed class PublicProductJson
    {
        public string uniqueId { get; set; } = string.Empty;
        public string? description { get; set; }
        public string? composition { get; set; }
    }
#pragma warning restore CS0649
}
