using EliteRestaurantPro.Data;

var path = AppDbContext.DatabasePath;
var removed = AppDbContext.DeleteAllActiveOrders();
Console.WriteLine($"Database: {path}");
Console.WriteLine($"Removed {removed} active order(s) (Waiting / In Kitchen / Ready).");
