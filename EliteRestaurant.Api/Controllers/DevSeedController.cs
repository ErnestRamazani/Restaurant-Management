using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

/// <summary>Development-only helpers (demo data). Not available in Production.</summary>
[ApiController]
[Route("api/dev")]
public sealed class DevSeedController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("seed-demo-clients")]
    public ActionResult<object> SeedDemoClients()
    {
        if (!env.IsDevelopment())
            return NotFound();

        var result = DemoClientHistorySeed.Ensure(db);
        return Ok(new
        {
            result = result.ToString(),
            database = AppDbContext.GetDatabaseTargetDescription(),
            message = result switch
            {
                DemoClientHistorySeed.EnsureResult.Seeded =>
                    "15 demo clients created (CLT-DEMO-*). Refresh Clients in Elite Pro.",
                DemoClientHistorySeed.EnsureResult.RepairedTenantScope =>
                    "Existing demo clients repaired for tenant scope. Refresh Clients.",
                DemoClientHistorySeed.EnsureResult.AlreadyPresent =>
                    "Demo clients already exist for this database.",
                _ => "Need products, tables, servers, and a restaurant row in this database."
            }
        });
    }
}
