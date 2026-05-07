# Cloud Migration Inventory

This inventory captures the main localhost/LAN and direct-database coupling points that must be migrated for the cloud architecture.

## API Surface

Current controllers:

- `api/auth`: staff PIN login using an opaque session token from `TabletAuthService`.
- `api/public/menu`: public customer menu, table list, and customer draft submit.
- `api/server`: server/cashier order creation, drafts, ready orders, assets, open-check checks.
- `api/cashier`: cashier queues, active/past orders, release/cancel/complete.
- `api/tables`: table list filtered for current server.
- `api/reservations`: arrived reservations for order source selection.
- `api/health`: health and database smoke check.

Current SignalR:

- `OrderHub` at `/hubs/order`.
- Current group join is client-driven (`JoinServer`) and must become authenticated/claim-driven before internet exposure.

## Web Surfaces

Current web entry points:

- `elite-menu`: Vite/React customer menu SPA, built into `EliteRestaurant.Api/wwwroot/menu`.
- `EliteRestaurant.Api/wwwroot/index.html`: server portal.
- `EliteRestaurant.Api/wwwroot/cashier.html`: cashier portal.
- `EliteRestaurant.Api/wwwroot/cashier-order.html`: compact cashier ordering portal.

Current API URL pattern:

- Browser pages mostly use same-origin relative `/api/...`.
- `elite-menu/src/utils/api.js` hardcodes `/api/public/menu`.
- Vite dev proxies `/api` to `http://localhost:5223`.

## WPF Direct Database Usage

These WPF classes create `AppDbContext` directly and must be migrated behind API clients screen by screen:

- `AdminLoginViewModel`
- `AdminDashboardViewModel`
- `AdminOrdersViewModel`
- `AppearanceSettingsViewModel`
- `AttendanceViewModel`
- `CreateOrderViewModel`
- `EmployeesViewModel`
- `InventoryViewModel`
- `KitchenOrdersViewModel`
- `MenuViewModel`
- `MoneyViewModel`
- `OrderDetailPanelViewModel`
- `ReportsViewModel`
- `ReservationsViewModel`
- `SalaryViewModel`
- `ServerPickupViewModel`
- `StaffLoginViewModel`
- `TablesViewModel`
- Services: `AdminOrdersSnapshotLoader`, `OrderSubmissionService`, `TableLoadingService`, `MoneyFinancialPdfExportService`, `FinancialPostingService`

## Core Server-Side Transaction Logic

These should remain server-side and be called by API endpoints rather than reimplemented in the WPF client:

- `EliteRestaurant.Core/Orders/AdminOrderOperationsService.cs`
- `EliteRestaurant.Core/Data/OrderInventoryDeduction.cs`
- `EliteRestaurant.Core/Data/DataReconciler.cs`
- `EliteRestaurant.Core/Data/FinancialTransactionService.cs`
- `EliteRestaurant.Core/Reporting/*`

## Migration Boundaries

Recommended migration order:

1. Cloud-ready API hosting and secure configuration.
2. JWT/RBAC and authenticated SignalR.
3. Unified React web hub with dynamic API base URL.
4. Shared contracts/DTO project.
5. Admin API endpoints by domain.
6. WPF API client layer and screen-by-screen replacement of direct DB access.
