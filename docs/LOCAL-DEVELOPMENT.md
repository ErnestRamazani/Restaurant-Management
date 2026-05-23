# Local development (no cloud)

Work on your PC against **local PostgreSQL** and **local API**. Push to `main` when ready; DigitalOcean deploys the cloud app for customers.

## Architecture

| Piece | Local | Production (clients) |
|-------|--------|----------------------|
| API | `http://localhost:8080` | `https://etoilegourmandekin.com` |
| Database | `127.0.0.1:5432/elite_restaurant` | DigitalOcean Postgres |
| Desktop settings | `%LocalAppData%\EliteRestaurantPro\settings\` | Installed release profile |

Cloud and local data do **not** sync automatically. Local DB changes stay local until you deploy API code (schema via migrations).

## 1. PostgreSQL on your PC

Create database `elite_restaurant` (pgAdmin or `psql`).

**PostgreSQL password** — pick one:

1. **Easiest:** leave `DefaultConnection` empty in `appsettings.Development.json` (default). The API reuses the same password as **Elite Pro → Database** settings (`%LocalAppData%\EliteRestaurantPro\settings\app-settings.json`, DPAPI).
2. **Or:** copy `appsettings.Development.local.json.example` → `appsettings.Development.local.json` and paste your real password (this file is gitignored).
3. **Or:** set env var before starting the API:  
   `$env:ELITE_POSTGRES_CONNECTION = "Host=127.0.0.1;Port=5432;Database=elite_restaurant;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Prefer"`

If you see **`password authentication failed for user "postgres"`**, the API is using the wrong password — fix one of the options above (remove any `Password=postgres` placeholder you added).

## 2. Start the local API

```powershell
cd EliteRestaurant.Api
.\run-dev.ps1
```

- API: **http://localhost:8080**
- Swagger: **http://localhost:8080/swagger**
- Health: **http://localhost:8080/api/health**

`run-dev.ps1` stops a previous API process first (avoids DLL file-lock build errors).

## 3. Point Elite Pro at localhost

In the desktop app: **Settings → Cloud API URL** → `http://localhost:8080` (no trailing slash).

Or edit `%LocalAppData%\EliteRestaurantPro\settings\app-settings.json`:

```json
"cloudApi": {
  "baseUrl": "http://localhost:8080"
}
```

**Database** in desktop settings is only for optional local Postgres tools; daily use goes through the API.

## 4. Run the desktop app

**Close the API window first** if you need to rebuild, OR use two terminals:

```powershell
# Terminal A — API (already running from run-dev.ps1)

# Terminal B — desktop
cd C:\Users\ernes\Documents\EliteRestaurant
dotnet run --project .\EliteRestaurantPro\EliteRestaurantPro.csproj
```

If build fails with *"file is being used by another process"*, stop the API (`run-dev.ps1` does this) or close the API PowerShell window, then build again.

## 5. First restaurant on local DB

If the local database is empty:

1. Open **http://localhost:8080/api/setup/status** — should show `setupRequired: true`.
2. Use the desktop **first-time setup** wizard with Cloud API = `http://localhost:8080`.

In Development, the API picks the first restaurant automatically when no custom domain matches (`localhost`).

## 6. Push live updates (code only — not your local database)

When you `git push`, DigitalOcean deploys **application code** (API + static web files). It does **not** read or upload your local PostgreSQL database.

| What deploys | What does **not** deploy |
|--------------|---------------------------|
| C# / API binaries | Rows from local `elite_restaurant` (menu, orders, staff) |
| `wwwroot` HTML/JS/CSS | Your `%LocalAppData%\EliteRestaurantPro\` settings file |
| New **EF migration files** in the repo (see below) | Desktop “test” logos/backgrounds on disk |

**Two completely separate databases:**

```text
Local:       127.0.0.1:5432 / elite_restaurant     ← only you, only while developing
Production:  DigitalOcean managed Postgres           ← what live clients use
```

Nothing syncs between them unless **you** point Elite Pro at the production URL and use features that **save to cloud** (Appearance → push settings, orders, menu sync, etc.). Normal local dev uses `http://localhost:8080` so those writes stay local.

### The one production DB effect when you deploy **code**

On startup, the live API runs **EF Core migrations** against **production** Postgres. That updates **table structure** (columns, indexes) to match the new code — not your local test data.

Example: you add a new column in code → migration runs on DO → production gets the new column; your local DB gets it when you run the API locally too. Existing production **rows** (Étoile’s menu, orders) are not replaced by your local rows.

Sample/demo seeding is **off** (`BootstrapSampleData = false`), so deploy never bulk-inserts fake restaurants from the repo.

### Safe habit

While building features locally:

- Cloud API URL = `http://localhost:8080`
- Do **not** point at `https://etoilegourmandekin.com` unless you intentionally want to change live data

Steps:

1. Commit and push to `main`.
2. DigitalOcean rebuilds the API.
3. Ship a new desktop ZIP only when the **desktop app** changed: `.\scripts\publish-desktop-release.ps1`.

## elite-menu (Vite) proxy errors

`ECONNREFUSED` on `/api/...` means **the API is not running** on port **8080** (often because Postgres login failed).

1. Fix Postgres connection (above).
2. Start API: `cd EliteRestaurant.Api` → `.\run-dev.ps1`
3. Then start menu: `.\run-elite-menu-dev.ps1` (or `elite-menu\run-dev.ps1`)

Vite at `http://localhost:5173` proxies `/api` → `http://localhost:8080`.

## Quick checklist

- [ ] Postgres running, `elite_restaurant` exists
- [ ] `appsettings.Development.json` connection string correct
- [ ] `.\run-dev.ps1` — API healthy on :8080
- [ ] Desktop Cloud API = `http://localhost:8080`
- [ ] Not using production URL while testing local DB
