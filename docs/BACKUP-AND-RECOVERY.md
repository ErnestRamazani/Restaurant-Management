# Backup and recovery (PostgreSQL)

Operational risk: data loss from an accidental `SeedRunner` run, disk failure, or power loss with no backup is high severity. Use automated backups and verify restores periodically.

## Daily automated backup (Windows Task Scheduler)

### Backup script

Use `scripts/backup-postgres.bat` after editing connection variables at the top of the file (or set `PGPASSWORD` / paths via environment).

The script:

- Runs `pg_dump` in custom format (`.dump`)
- On success, deletes backups older than 14 days in the backup folder

Schedule it in **Task Scheduler** to run daily (for example at 2:00 AM).

### Storage

Store backups on a **different physical drive or network share**. A backup on the same disk as the database does not protect against disk failure.

## Restore procedure

From a `.dump` file (adjust host, user, database name to match your environment):

```text
pg_restore ^
  --host=localhost ^
  --port=5432 ^
  --username=elite_user ^
  --dbname=elite_restaurant ^
  --clean ^
  --if-exists ^
  elite-backup-2026-04-23_02-00.dump
```

On Linux/macOS, use `\` line continuation instead of `^` as appropriate.

## Recovery time objective (RTO)

Target: **under 30 minutes** for a full restore from the last good nightly backup, assuming backup media and credentials are available.

## Recovery point objective (RPO)

Target: **under 24 hours** of potential data loss when relying on a single nightly backup.

For stricter RPO, enable PostgreSQL WAL archiving (point-in-time recovery, PITR).

## Before running SeedRunner

1. Confirm you have a recent backup: list files in your backup directory (for example `dir C:\EliteRestaurant\backups\*.dump` on Windows).
2. Confirm the connection string points at the **intended** database (not production if you meant staging).
3. Proceed only after explicit confirmation; destructive reseeds should never run against production without a fresh backup.
