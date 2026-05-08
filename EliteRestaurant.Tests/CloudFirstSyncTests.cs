using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class CloudFirstSyncTests
{
    [Fact]
    public void SaveChanges_WhenCloudFails_StillSavesLocalBackupAndQueuesOutbox()
    {
        var options = CreateOptions();
        AppDbContext.CloudSyncDispatcher = (_, _) => throw new HttpRequestException("offline");

        try
        {
            using (var db = new AppDbContext(options))
            {
                db.Products.Add(new Product
                {
                    UniqueId = "prod-test",
                    Name = "Test Product",
                    Category = "Main",
                    SubCategory = "General",
                    Price = 10m
                });

                db.SaveChanges();
            }

            using var verify = new AppDbContext(options);
            Assert.Equal(1, verify.Products.Count());
            var queued = Assert.Single(verify.SyncOutbox);
            Assert.Equal(nameof(Product), queued.EntityName);
            Assert.Equal("Upsert", queued.Operation);
            Assert.Equal("Pending", queued.Status);
            Assert.Contains("offline", queued.LastError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppDbContext.CloudSyncDispatcher = null;
        }
    }

    [Fact]
    public void SaveChanges_WhenCloudSucceeds_DoesNotQueueOutbox()
    {
        var options = CreateOptions();
        AppDbContext.CloudSyncDispatcher = (operations, _) =>
            Task.FromResult<IReadOnlyList<CloudSyncResult>>(
                operations.Select(o => new CloudSyncResult(o.IdempotencyKey, true, "ok")).ToList());

        try
        {
            using (var db = new AppDbContext(options))
            {
                db.InventoryItems.Add(new InventoryItem
                {
                    UniqueId = "inv-test",
                    Name = "Tomatoes",
                    Unit = "kg",
                    StockQuantity = 12m
                });

                db.SaveChanges();
            }

            using var verify = new AppDbContext(options);
            Assert.Equal(1, verify.InventoryItems.Count());
            Assert.Empty(verify.SyncOutbox);
        }
        finally
        {
            AppDbContext.CloudSyncDispatcher = null;
        }
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
}
