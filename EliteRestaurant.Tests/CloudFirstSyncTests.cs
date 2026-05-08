using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EliteRestaurant.Tests;

public sealed class CloudFirstSyncTests
{
    [Fact]
    public void SaveChanges_QueuesCloudSyncWithoutBlockingLocalBackup()
    {
        var options = CreateOptions();
        var notified = false;
        AppDbContext.CloudSyncQueued = () => notified = true;

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
            Assert.Contains("Queued", queued.LastError, StringComparison.OrdinalIgnoreCase);
            Assert.True(notified);
        }
        finally
        {
            AppDbContext.CloudSyncQueued = null;
        }
    }

    [Fact]
    public void SaveChanges_WithoutSyncNotifier_DoesNotQueueOutbox()
    {
        var options = CreateOptions();
        AppDbContext.CloudSyncQueued = null;
        AppDbContext.CloudSyncDispatcher = null;

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

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
}
