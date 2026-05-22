-- DESTRUCTIVE: removes ALL application data (every tenant). Keeps schema + __EFMigrationsHistory.
-- Run via psql on your PC (see docs/RESET-PRODUCTION-DATABASE.md) or any PostgreSQL client.
-- After this: GET /api/setup/status should show setupRequired=true

TRUNCATE TABLE
    "OrderItems",
    "Orders",
    "ProductIngredients",
    "Products",
    "InventoryItems",
    "Tables",
    "EmployeeAttendances",
    "SalaryAdvances",
    "PayrollPaymentRecords",
    "Employees",
    "Transactions",
    "CustomerProfiles",
    "ReservationEngagements",
    "Reservations",
    "PlacementUnits",
    "WaitlistEntries",
    "SharedOrderDrafts",
    "TabletSessions",
    "SyncOutbox",
    "PublicMenuAssets",
    "PublicMenuSettings",
    "AttendanceDayValidations",
    "Restaurants"
RESTART IDENTITY CASCADE;

SELECT 'Restaurants' AS "Table", COUNT(*) AS "Rows" FROM "Restaurants"
UNION ALL SELECT 'Employees', COUNT(*) FROM "Employees"
UNION ALL SELECT 'Orders', COUNT(*) FROM "Orders";
