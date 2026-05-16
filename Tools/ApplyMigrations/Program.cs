using EliteRestaurant.Core.Data;

DatabaseMigrationRunner.ApplyPendingMigrations();
Console.WriteLine("Done. Pending EF migrations were applied (if any).");
