using EliteRestaurant.Core.Data;

DatabaseInitializer.Initialize();

Console.WriteLine($"Database: {AppDbContext.GetDatabaseTargetDescription()}");

var removed = DataReconciler.DeleteAllActiveOrders();
Console.WriteLine($"Removed {removed} active order(s) (Waiting / In Kitchen / Ready / Served).");
