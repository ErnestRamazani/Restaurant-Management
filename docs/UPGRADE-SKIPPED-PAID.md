# Upgrade guide — deferred (paid / external-only)

These items from [UPGRADE-GUIDE-70-75.md](./UPGRADE-GUIDE-70-75.md) were **intentionally not implemented** in code because they require paid infrastructure or changes only in external consoles.

| Item | Est. cost | Enable when |
|------|-----------|-------------|
| DigitalOcean Managed Redis + SignalR backplane | ~$15/mo | You need 2+ API instances |
| Second App Platform instance (`basic-xs` × 2) | ~$12/mo | After Redis backplane |
| DigitalOcean Spaces + `ContentUrl` for menu images | ~$5/mo | Guest QR / image load becomes heavy |
| Staging app + separate Postgres | ~$7/mo | You want branch deploy before `main` |
| PITR on managed Postgres | paid tier | RPO tighter than daily backup |
| Logtail paid retention | ~$10/mo | Long searchable log history |
| UptimeRobot / DO backup toggles / sticky sessions | free–console | Ops checklist only |

## Already done in repo (free tier)

- Sentry (when `Sentry:Dsn` is set)
- CORS whitelist, rate limits, HSTS, `/api/health/db` protected
- Migrations guarded to Development startup; pool tuning on connection string
- Six API logic fixes, portal UX (cancel passcode, API error banner, SignalR banner)
- CI: Dependabot, 35% coverage gate, production smoke `curl` on `main`

## Recommended order when budget allows

1. Verify DO **daily backups** in console  
2. **Redis** → SignalR backplane in `Program.cs`  
3. **`instance_count: 2`** in `.do/app.yaml`  
4. **Spaces** for `PublicMenuAsset` binaries  
5. **Staging** app on `develop` branch  
