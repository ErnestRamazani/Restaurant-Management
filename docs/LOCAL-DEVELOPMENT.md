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

Edit **`EliteRestaurant.Api/appsettings.Development.json`** — set `ConnectionStrings:DefaultConnection` to your local user/password (file is gitignored patterns: use your real password locally only).

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

## 6. Push live updates

1. Commit and push to `main`.
2. DigitalOcean rebuilds the API (same codebase, production connection string + domains).
3. Ship a new desktop ZIP only when the **desktop app** changed: `.\scripts\publish-desktop-release.ps1`.

## Quick checklist

- [ ] Postgres running, `elite_restaurant` exists
- [ ] `appsettings.Development.json` connection string correct
- [ ] `.\run-dev.ps1` — API healthy on :8080
- [ ] Desktop Cloud API = `http://localhost:8080`
- [ ] Not using production URL while testing local DB
