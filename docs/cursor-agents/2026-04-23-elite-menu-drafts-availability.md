# Agent session — 2026-04-23 (elite-menu + API)

Short-lived handoff: what we changed and where. Add more `cursor-agents/*.md` files for other sessions to keep a local trail next to the repo.

## Customer menu (elite-menu + PublicMenuController)

- **Occupied tables + labels:** `GET /api/public/menu/tables` includes `Available` and `Occupied` (still excludes `Maintenance`) so the cart still resolves table name and server after an order.
- **Out-of-stock items:** `GET /api/public/menu/products` returns all products; `isAvailable` from ingredient stock. UI: `ProductCard` / `ProductSheet` / `useCart`; out-of-order styling and red “Out of order” label.
- **Details for unavailable items:** Tapping a card still opens the detail sheet; add/cart remains blocked.
- **Helpers:** `elite-menu/src/utils/availability.js`, `QuantityControl` `disablePlus` where needed.

## Drafts bound to table (core + WPF + server portal)

- **Model / migration:** `SharedOrderDraft.TableId` (migration `20260423120000_AddSharedOrderDraftTableId`). Apply via normal app startup (`DatabaseInitializer`) or `dotnet ef database update` on `EliteRestaurant.Core`.
- **Store:** `SharedOrderDraftStore.ListServerDrafts(employeeId, selectedTableId, restrictCustomerToAssigned…)`; customer drafts (`EmployeeId == 0`) only for the selected table. Server role (API) / server tablet (WPF) can restrict to assigned table.
- **WPF:** `CreateOrderViewModel` / `DraftPersistenceService` pass `SelectedTableId` and refresh on table change.
- **API:** `GET/DELETE /api/server/drafts?tableId=…`; web: `index.html` / `cashier.html` pass `tableId` on load and delete.

## Files touched (non-exhaustive)

- `EliteRestaurant.Api/Controllers/PublicMenuController.cs`, `ServerPortalController.cs`
- `EliteRestaurant.Core/Utils/SharedOrderDraftStore.cs`, `Models/SharedOrderDraft.cs`, migrations + snapshot
- `EliteRestaurantPro/…/CreateOrderViewModel.cs`, `DraftPersistenceService.cs`
- `elite-menu/…/ProductCard.jsx`, `ProductSheet.jsx`, `useCart.js`, `QuantityControl.jsx`
- `EliteRestaurant.Api/wwwroot/index.html`, `cashier.html`

## Note

This file is a **project** bookmark. Cursor’s own **Agents / chat history** still lives in the IDE (see Cursor docs for your version if you need export).