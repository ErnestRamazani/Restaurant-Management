# Free-tier operations checklist

Complete these once per production environment. No paid services required.

## Error tracking — Sentry (free tier)

1. Create a project at [sentry.io](https://sentry.io) (free tier, no credit card).
2. In DigitalOcean App Platform → your API app → **Environment Variables**, add:
   - `Sentry__Dsn` = your project DSN
3. Redeploy. Trigger a test error or wait for the first real unhandled exception.
4. Confirm events appear in Sentry with environment name `Production`.

Code is already wired: `UseSentry` runs when `Sentry:Dsn` is set.

## Availability — UptimeRobot (free)

1. Sign up at [uptimerobot.com](https://uptimerobot.com).
2. Add an HTTP(s) monitor:
   - URL: `https://starfish-app-owtoz.ondigitalocean.app/api/health` (or your custom domain)
   - Interval: 5 minutes
   - Alert: email (optional SMS)
3. Document the alert contact in your grad report.

## Database backups — DigitalOcean console

1. **Databases** → your Postgres cluster → **Backups**.
2. Confirm **daily backups** are enabled and note retention (e.g. 7 days).
3. Optional: enable PITR if your tier includes it (see [RESTORE-RUNBOOK.md](./RESTORE-RUNBOOK.md)).
4. Screenshot or note the setting for evidence — no repo change required.

## Logs — DigitalOcean runtime

1. App Platform → API component → **Runtime Logs**.
2. On incidents: check logs after UptimeRobot alert, then Sentry for stack traces.

## Migrations — before deploy

Production does **not** migrate on startup. Before each release:

```bash
dotnet ef database update --project EliteRestaurant.Core --startup-project EliteRestaurant.Api
```

Or set GitHub secret `PROD_DB_CONNECTION` so CI `migrate-check` applies pending migrations on `main` push.
