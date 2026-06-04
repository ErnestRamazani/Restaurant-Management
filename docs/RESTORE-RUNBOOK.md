# EliteRestaurant — Database restore runbook (DigitalOcean Managed PostgreSQL)

Use this when production data must be recovered from a managed backup or PITR snapshot.

## Prerequisites

- Access to the DigitalOcean account (Databases + App Platform).
- The **connection string** for the target cluster (read/write).
- A maintenance window (expect 5–15 minutes of API downtime while `DATABASE_URL` is switched).

## Restore from a daily backup

1. In **DigitalOcean → Databases → your cluster → Backups**, pick the snapshot date/time.
2. Choose **Restore** (creates a **new** cluster from that backup — DO does not overwrite the live cluster in place).
3. When the new cluster is **online**, copy its connection string from **Connection Details**.
4. In **App Platform → elite-api → Settings → App-Level Environment Variables**, update `DATABASE_URL` to the new cluster string.
5. Trigger a **Redeploy** (or wait for the next deploy). The API will connect to the restored database.
6. Verify (see below). Decommission the old broken cluster when satisfied.

## Point-in-time recovery (PITR)

If PITR is enabled on your tier:

1. Open the database cluster → **Backups / Point-in-time**.
2. Select the timestamp **just before** the incident (accidental wipe, bad migration, etc.).
3. Restore to a new cluster and follow steps 4–6 above.

## Verification queries

Connect with `psql` or any SQL client using the new `DATABASE_URL`:

```sql
SELECT COUNT(*) AS orders FROM "Orders";
SELECT MAX("CreatedAt") AS last_order FROM "Orders";
SELECT COUNT(*) AS employees FROM "Employees";
```

Compare counts and `last_order` to what you expect for the restaurant.

Hit the public health endpoint:

```bash
curl -f https://starfish-app-owtoz.ondigitalocean.app/api/health
```

Sign in to the **admin web** portal and confirm recent orders appear.

## Rollback

If the restored cluster is wrong, repeat the procedure with an earlier backup or PITR time. Keep the previous cluster until verification passes.

## Related

- Automated backups: enable in DO console (no app code change).
- Migrations: run via CI or `dotnet ef database update` **before** deploy; production API no longer migrates on startup.
