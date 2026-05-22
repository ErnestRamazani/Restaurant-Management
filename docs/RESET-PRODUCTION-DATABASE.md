# Reset production database (empty all tenants)

Use this when you want a **clean production start** — e.g. run the desktop **First-time setup** wizard with `etoilegourmandekin.com` as the only restaurant.

**This does not delete the DigitalOcean database cluster** — only all rows in application tables. Schema and migrations stay.

## Option A — DigitalOcean SQL console (recommended)

1. DigitalOcean → **Databases** → `elite-restaurant-db` → **Connection details** → open **Console** (or use psql).
2. Paste and run: [`scripts/wipe-all-tenant-data.sql`](../scripts/wipe-all-tenant-data.sql)
3. Confirm the final `SELECT` shows **0** rows for Restaurants, Employees, Orders.

## Option B — From your PC

```powershell
$env:ELITE_POSTGRES_CONNECTION = "<paste DO connection string; SSL Require>"
cd C:\Users\ernes\Documents\EliteRestaurant
.\scripts\reset-cloud-database.ps1
```

When prompted, type `WIPE` in the tool after `WIPE_ALL_DATA` in the script.

## After wipe

1. Restart the API app on App Platform (or wait for next deploy).
2. Check: `GET https://etoilegourmandekin.com/api/setup/status` → `"setupRequired": true`
3. Run `publish\EliteRestaurantPro-first-run\EliteRestaurantPro.exe`
4. Cloud API URL: `https://etoilegourmandekin.com` (or starfish URL)
5. Custom domain: `etoilegourmandekin.com`
6. **Create first site & sign in** (not “add new restaurant” — DB is empty)

## Local desktop settings

Delete or reset portable settings so the wizard opens again:

- `publish\EliteRestaurantPro-first-run\app-settings.json` → `"firstSiteSetupCompleted": false`
- Or delete `%LocalAppData%\EliteRestaurantPro\settings\app-settings.json` if not using the portable build

## What is not wiped

- Uploaded files on disk (if any) under API `wwwroot` — clear separately if needed
- DigitalOcean App env vars (`DATABASE_URL`, JWT, etc.)
- DNS / custom domains in App Platform
