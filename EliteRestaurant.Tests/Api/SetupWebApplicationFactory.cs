using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EliteRestaurant.Tests.Api;

/// <summary>Isolated in-memory DB for setup API tests (empty until first-site runs).</summary>
public sealed class SetupWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"SetupApiTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<EliteRestaurant.Core.Data.AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<EliteRestaurant.Core.Data.AppDbContext>(o =>
                o.UseInMemoryDatabase(_databaseName));
        });
    }
}
