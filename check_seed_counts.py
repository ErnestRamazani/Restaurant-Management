import os
import sqlite3


db_path = os.path.join(
    os.environ["LOCALAPPDATA"],
    "EliteRestaurantPro",
    "elite-restaurant-pro.db",
)

con = sqlite3.connect(db_path)
cur = con.cursor()

for table in ["Employees", "Products", "Tables", "InventoryItems", "Orders", "OrderItems", "EmployeeAttendances", "Transactions"]:
    count = cur.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
    print(f"{table}: {count}")

first_order = cur.execute("SELECT MIN(CreatedAt), MAX(CreatedAt) FROM Orders").fetchone()
print("Orders range:", first_order)

prepared = cur.execute("SELECT COUNT(*) FROM OrderItems WHERE PreparedByRole <> ''").fetchone()[0]
print("Prepared order items:", prepared)

con.close()
