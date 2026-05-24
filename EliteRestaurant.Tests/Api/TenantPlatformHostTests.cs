using System.Net;
using System.Net.Http.Json;
using EliteRestaurant.Contracts.Auth;
using EliteRestaurant.Contracts.Setup;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EliteRestaurant.Tests.Api;

public sealed class TenantPlatformHostTests
{
    [Fact]
    public async Task AuthLogin_OnPlatformHost_ResolvesTenantWithoutHeaders()
    {
        using var factory = new SetupWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "starfish-app-owtoz.ondigitalocean.app";

        await client.PostAsJsonAsync(
            "/api/setup/first-site",
            new SiteSetupRequest(
                "Platform Host Bistro",
                "platform-bistro",
                null,
                "platformadmin",
                "1234",
                "Platform Admin",
                "en"));

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new CloudLoginRequest("platformadmin", "1234", "Admin"));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<CloudLoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }
}
