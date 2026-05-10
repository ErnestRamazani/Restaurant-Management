# PostgreSQL: local to cloud

This project uses **EF Core** migrations in `EliteRestaurant.Core` and **Npgsql**. Desktop (WPF), API, and tools resolve the connection string in this order (see `AppDbContext.TryGetPostgreSqlConnectionString`):

1. **`DATABASE_URL`** — typical on Heroku, Railway, Render, DigitalOcean App Platform, Fly.io. SSL parameters are normalized for cloud hosts when needed.
2. **`ELITE_POSTGRES_CONNECTION`** — full Npgsql-style connection string (preferred when `DATABASE_URL` is awkward).
3. **`ConnectionStrings__DefaultConnection`** — standard .NET environment variable mapping for `ConnectionStrings:DefaultConnection`.
4. **`appsettings.{Environment}.json` / `appsettings.json`** — `ConnectionStrings:DefaultConnection` (omit secrets from source control; use user secrets or deployment env vars).
5. **Desktop `app-settings.json`** — database host/port/name/user/password saved by WPF (under `%LocalAppData%\EliteRestaurantPro\settings\`).

Never commit real passwords. Copy from `EliteRestaurant.Api/appsettings.Template.json` locally as `appsettings.Development.json` (gitignored if you choose) or inject secrets only via the host environment.

## Apply migrations to the cloud database

From the repository root (adjust startup project if needed):

```bash
dotnet ef database update --project EliteRestaurant.Core --startup-project EliteRestaurant.Api
```

The API also runs `DatabaseInitializer.Initialize()` on startup (non-Testing), which applies pending migrations when a connection string is present.

## Logical backup / restore (pg_dump / pg_restore)

Use **custom-format** dumps for flexibility. Replace placeholders with your real hosts and credentials (do not paste secrets into the repo).

**Dump (source — e.g. local PostgreSQL):**

```bash
pg_dump -h SOURCE_HOST -p 5432 -U SOURCE_USER -d SOURCE_DB -Fc -f elite_restaurant.dump
```

**Restore (target — e.g. Supabase, Azure Database for PostgreSQL, AWS RDS):**

Create an empty database on the provider first if required. Then:

```bash
pg_restore -h TARGET_HOST -p 5432 -U TARGET_USER -d TARGET_DB --no-owner --no-privileges --clean --if-exists elite_restaurant.dump
```

Notes:

- Managed clouds often require **SSL** (`SSL Mode=Require` in Npgsql, or parameters added by `DATABASE_URL`).
- **`--clean --if-exists`** drops objects before recreate; ensure you are pointing at the correct target.
- If restore fails on extensions or roles, create the extension in the provider dashboard (e.g. `uuid-ossp`) and retry without global privileges.

## Provider-specific hints

| Provider | Typical configuration |
|----------|------------------------|
| **Supabase** | Connection string in project settings; use SSL; pooler URL vs direct URL affects port (5432 vs 6543). |
| **Azure Database for PostgreSQL** | Firewall rules or “Azure services”; SSL required. |
| **AWS RDS** | Security groups open to API host; master user for restore then least-privilege app user. |
| **DigitalOcean Managed DB** | Connection details + CA; App Platform often injects `DATABASE_URL`. |

## After cutover

1. Point **`DATABASE_URL`** or **`ELITE_POSTGRES_CONNECTION`** at the cloud instance where the API runs.
2. Point desktop **Database** settings (or env vars on dev machines) at the same database if the POS should share data with the cloud API.
3. Re-save **Appearance → Business profile** in WPF once so branding pushes to `PublicMenuSettings` / `PublicMenuAssets` if the cloud DB was empty.

See also `scripts/pg-cloud.example.ps1` for placeholder-only examples.
