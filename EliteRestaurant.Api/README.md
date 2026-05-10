# EliteRestaurant.Api

## PostgreSQL connection (no secrets in git)

The API reads configuration from environment variables and optional local `appsettings.*.json`. For deployment, prefer:

- **`DATABASE_URL`** — common on PaaS hosts.
- **`ELITE_POSTGRES_CONNECTION`** — Npgsql connection string if you are not using `DATABASE_URL`.
- **`ConnectionStrings__DefaultConnection`** — alternative env mapping.

Local template without committing credentials: copy `appsettings.Template.json` patterns into your own untracked file or user secrets.

Full migration and `pg_dump` / `pg_restore` guidance: `docs/postgresql-cloud-deployment.md`.
