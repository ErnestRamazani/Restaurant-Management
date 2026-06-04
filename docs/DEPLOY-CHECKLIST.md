# Production deploy checklist

Run this before switching traffic to a new API build.

## 1. Database migrations (required)

Migrations run automatically only in **Development**. Production must apply them explicitly:

```bash
dotnet ef database update --project EliteRestaurant.Core --startup-project EliteRestaurant.Api
```

Or set `ConnectionStrings__DefaultConnection` / `DATABASE_URL` and run the same from your deploy host.

Verify pending migrations:

```bash
dotnet ef migrations list --project EliteRestaurant.Core --startup-project EliteRestaurant.Api
```

Recent remediation migrations include `AddOrderItemInventoryDeductedAt`, `BackfillOrderItemInventoryDeductedAt`, and `AddOrderMerchandiseGrandTotalUsd`.

## 2. Health checks

| Endpoint | Auth | Expect |
|----------|------|--------|
| `GET /api/health` | None | `200` |
| `GET /api/health/db` | Admin JWT | `200` with token, `401` without |

## 3. CORS and CSP

Set `Cors:AllowedOrigins` in `appsettings.json` or environment variables (`Cors__AllowedOrigins__0`, …) for every staff/guest origin:

- DigitalOcean app URL
- Custom domain (with and without `www` if used)
- Local dev ports (`8080`, `5173`) for tablets on LAN if applicable

CSP `connect-src` is built from the same origin list for static portals.

## 4. Secrets and config

- `Jwt:SigningKey` — production secret (32+ chars)
- `ConnectionStrings:DefaultConnection` or `DATABASE_URL`
- `Sentry:Dsn` — optional; omit in dev/test if unused
- `SETUP_ALLOW_WIPE` — never set in production unless intentionally wiping

## 5. Post-deploy smoke (manual)

1. Anonymous `/api/health` returns 200.
2. Server login → cancel order with passcode (no 403).
3. Cashier release → append to in-kitchen check → release again (stock not double-deducted).
4. Complete order with change → Money report sale matches merchandise grand.

## 6. Free-tier ops (no cost — rubric)

See [FREE-TIER-OPS.md](./FREE-TIER-OPS.md) for Sentry DSN, UptimeRobot monitor, DO backup verification, and log triage. These are console/signup steps, not application code.
