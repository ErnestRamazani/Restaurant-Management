# Customer Menu System

## How it works

1. The **public menu** is a React app built into `EliteRestaurant.Api/wwwroot/menu/`. The API serves it as static files at **`/menu/`** (same host and port as the API, e.g. `http://localhost:5223/menu/`).
2. Guests open that URL, often from a **QR code** on the table (`?table={id}`). The app calls **unauthenticated** JSON endpoints under **`/api/public/menu/*`** for branding, products, and tables, then **POST `/api/public/menu/draft`** to create a `SharedOrderDraft` (`Portal = "Customer"`, `EmployeeId = 0`).
3. Staff see new customer drafts in the server app; SignalR raises **`CustomerDraftArrived`** on group **`Server`** with draft id, label, table, and totals.
4. QR guests can tap **Call your Server** (when `?table=` is set). **POST `/api/public/menu/call-server`** broadcasts **`ServerTableCall`** to group **`Server`** (assigned server only when `AssignedServerId` is set). The server portal plays a ring tone and shows a toast.

**One source of truth:** Tax, service, currency, logo path, and business info come from **`SettingsManager`** (same as the WPF and server tools).

## QR codes

- **URL format:** `http://{host}:{port}/menu/?table={tableId}`  
  Example: `http://192.168.1.130:5223/menu/?table=3`
- **Configure the base** in WPF: **Settings → Business Profile** → **Public menu base URL** (e.g. `http://192.168.1.130:5223` in production, or LAN IP + port **5173** while Vite dev is running with `server.host: true`).
- **Settings → Menu QR Codes:** per-table QR images and **Print all QR codes (PDF)**.

## Building the frontend

```bash
cd elite-menu
npm install
npm run build
```

Output is written to **`EliteRestaurant.Api/wwwroot/menu/`** (see `elite-menu/vite.config.js`: `base: '/menu/'`, `build.outDir`).

From VS Code / Cursor: run the task **`build-menu`** (Terminal → Run Task) which runs `npm run build` in `elite-menu`.

## Development

```bash
cd elite-menu
npm run dev
```

- Vite dev server (default `http://localhost:5173/menu/`) **proxies `/api`** to **`http://localhost:5223`** so the app can call the real API.
- On Windows, **`elite-menu\run-dev.ps1`** opens a new PowerShell for Vite; **`.\run-dev.ps1 -Foreground`** runs in the current window. From repo root: **`.\run-elite-menu-dev.ps1`**.

**API must be running** (e.g. `EliteRestaurant.Api\run-dev.ps1` or `dotnet run --launch-profile http`) for menu + `/api` to work together in dev.

## API static files

`Program.cs` registers **`app.UseDefaultFiles()`** and **`app.UseStaticFiles()`** so `wwwroot` (including `wwwroot/menu/`) is served. The customer menu is available at:

`http://{api-host}:{port}/menu/`

**CORS:** The menu is typically same-origin as the API; no extra CORS is required. Optional origins can be set under **`Cors:AllowedOrigins`** if you host the menu elsewhere.

**Auth:** `PublicMenuController` is marked **`[AllowAnonymous]`**; public routes do not use tablet/session auth.

## Draft label convention

- **Prefix:** `🧑` (identifies customer-originated drafts in the server UI).
- **Format:** `🧑 Table {tableCode} — {customerName}` (table code from the `Table` record, not the raw id).

## What customers can and cannot do

| Can | Cannot |
|-----|--------|
| Browse menu, descriptions, composition (display fields) | Apply discounts or change tax/service rules |
| Add lines, notes, allergy info, submit a **draft** | Pay, choose payment method, or send straight to the kitchen as a final check |
| See estimated prep (same model as POS) where implemented | Act as staff: assign servers, use internal back-office tools |

## Database migrations

When the schema changes (e.g. product fields), apply migrations from the repo root:

```bash
dotnet ef database update --project EliteRestaurant.Core --startup-project EliteRestaurant.Api
```
