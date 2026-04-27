# Database setup (PostgreSQL)

This project uses PostgreSQL for production data. Use **least-privilege roles**: the running app should not use a superuser.

## Roles

### Application runtime role (`elite_app`)

Used by the WPF desktop and the API for normal operation: CRUD only, no schema changes, no `TRUNCATE`, no `DROP`.

Run as a PostgreSQL superuser (example names — adjust passwords and database name):

```sql
CREATE ROLE elite_app LOGIN PASSWORD 'strong_app_password';
GRANT CONNECT ON DATABASE elite_restaurant TO elite_app;
GRANT USAGE ON SCHEMA public TO elite_app;
GRANT SELECT, INSERT, UPDATE, DELETE
  ON ALL TABLES IN SCHEMA public TO elite_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO elite_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO elite_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO elite_app;
```

New tables created later by migrations still need default privileges (above) so `elite_app` keeps access; re-run `ALTER DEFAULT PRIVILEGES` after schema work if your migration user uses a different role name.

### Migration / admin role (`elite_admin`)

Used only for EF Core migrations, `SeedRunner`, and maintenance: can create objects and run elevated operations.

```sql
CREATE ROLE elite_admin LOGIN PASSWORD 'strong_admin_password';
GRANT elite_app TO elite_admin;
GRANT CREATE ON SCHEMA public TO elite_admin;
GRANT TRUNCATE ON ALL TABLES IN SCHEMA public TO elite_admin;
```

Grant additional privileges your migration workflow requires (for example `REFERENCES` or ownership of new objects), keeping `elite_app` limited to DML.

## Connection strings

- **Application** (`app-settings.json` / `ELITE_POSTGRES_CONNECTION` for WPF and API):

  `Host=localhost;Port=5432;Database=elite_restaurant;Username=elite_app;Password=...`

- **Migrations / SeedRunner** (separate secret, never ship in tablet or client packages):

  `Host=localhost;Port=5432;Database=elite_restaurant;Username=elite_admin;Password=...`

With this split, a compromised app connection string cannot drop tables or truncate data; it can only perform normal DML.

## Backups

See [BACKUP-AND-RECOVERY.md](./BACKUP-AND-RECOVERY.md) for automated backup and restore.
