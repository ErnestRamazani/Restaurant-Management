# EliteRestaurant — Production Upgrade Guide
## Target: 70–75 / 100 in Every Category

> **Context:** This guide is written for a single local restaurant that gets busy on Friday nights — not for Uber-scale. Every recommendation is proportionate to that context. Nothing here requires a DevOps team or a cloud budget above ~$30–50/month.

---

## How to Read This Document

Each section states:
- **Current score** and **why**
- **What the target (70–75) actually means** for a local restaurant
- **Exactly what to change**, technically, with enough detail to implement it

Work through sections in this recommended order based on impact vs effort:

1. Availability & Recovery (most urgent — data loss risk)
2. Error Tracking & Logs (you need to know when it breaks)
3. Security & RLS (low effort, high payoff)
4. Rate Limiting (one file, 30 minutes)
5. Scaling + Load Balancing (same root cause — Redis)
6. Auth & Permissions (CORS fix + login brute-force)
7. API & Backend Logic (the 6 bugs)
8. Cloud & Compute (object storage for images)
9. CI/CD (coverage + staging)
10. Hosting & Deployment (migration safety, instance size)
11. Database & Storage (pool tuning, image migration)
12. Frontend (error states, tablet UX)

---

## 1. Frontend — 62 → 75

### Why it scored 62
The portals are functional and cover every staff role. The failure points are: the cancel flow uses `window.confirm()` which is silently suppressed on tablet kiosk mode, there are no loading or error states for failed API calls, the localization was reverted and the portals are English-only hardcoded strings, and there is no graceful handling when SignalR disconnects mid-service.

### What 75 means here
A Friday night service does not crash visually. When the API is slow, staff sees a spinner. When it fails, they see a clear message. When the tablet loses WiFi, SignalR reconnects silently. The cancel flow always works regardless of browser mode.

### What to change

**Fix the cancel confirmation flow.**
Remove the `window.confirm()` call at the top of `cancelStaffOrder` in `wwwroot/shared/order-cancel-passcode.js`. The existing passcode modal that already follows it is sufficient confirmation — the `window.confirm()` step is redundant and fails on standalone/fullscreen PWA mode (Chrome on Android tablets suppresses it silently). The flow should be: tap "Cancel order" → passcode modal appears → enter passcode → submit. No `confirm()` needed.

**Add a global API error handler in each portal.**
Every portal has an `api()` helper function. That function currently returns the raw response or throws. Wrap it so that on any non-2xx response it posts a dismissible error banner at the top of the page (a `<div id="api-error-banner">` already styled in each portal's CSS). This means a kitchen tablet that gets a 500 from the server shows "Something went wrong — refresh or call support" instead of silently doing nothing. This is one code change per portal in the `api()` function.

**Add a loading indicator for every button that triggers an API call.**
The pattern is: on click, disable the button and change its text to "..." or "Saving…", then re-enable it when the request completes or fails. This prevents double-taps on tablet (a server accidentally submitting the same order twice on a busy night). This is a UI discipline issue, not an architecture one — apply it to the submit-order, serve, and mark-ready buttons at minimum.

**Handle SignalR disconnection gracefully.**
Each portal connects to SignalR on load and joins its group. When the connection drops (tablet WiFi hiccup), the portal currently shows stale data silently. Add a reconnect handler: on `connection.onclose`, show a yellow banner "Connection lost — reconnecting…" and call `connection.start()` in a retry loop with 5-second backoff. On `connection.onreconnected`, hide the banner and refresh the current view. The SignalR JS client has built-in `withAutomaticReconnect()` — enable it when building the `HubConnectionBuilder`.

**Add `data-i18n` to the admin login form properly (already partially done).**
The admin portal already has `EliteI18n` wired for the login screen. Verify that every string on the login form — label text, button text, error messages, the language switcher — uses `data-i18n` attributes and that `EliteI18n.applyToDocument()` is called on `elite-language-changed`. This is the one screen where localization already works; making it complete gets the admin portal to a consistent state.

---

## 2. API & Backend Logic — 65 → 75

### Why it scored 65
Clean structure, good middleware, proper EF patterns. Brought down by 6 confirmed logic bugs and the absence of a global exception handler.

### What 75 means here
The 6 critical bugs are fixed. The API never returns an unhandled stack trace to clients. The cancel endpoint has proper role guards.

### What to change

**Fix Bug #1 — Double inventory deduction on server append.**
When the server portal appends items to an order that is already `Waiting`, `In Kitchen`, `Ready`, or `Served` and resets it to `PendingCashier`, the subsequent cashier release calls `TryApplyForPlacedOrder` which deducts ALL items again including those already deducted on the first release. The fix: when the server portal appends to an in-progress order, it must immediately call `OrderInventoryDeduction.TryApplyForAdditionalItems` for only the new lines (the same method the admin portal correctly uses), and then mark those items as "already deducted" so `TryReleasePendingToKitchen` skips them. The simplest implementation is to track deducted items via a boolean flag on `OrderItem` (e.g. `InventoryDeductedAt`), and have `TryApplyForPlacedOrder` skip items where that flag is already set.

**Fix Bug #2 — Cross-tenant draft leak in SharedOrderDraftStore.**
`SharedOrderDraftStore` uses `new AppDbContext()` which constructs a `NullTenantContext`. Because `NullTenantContext.IsResolved = false`, the EF query filter (`!IsResolved || RestaurantId == x`) passes ALL records. Convert `SharedOrderDraftStore` from a static class to an injectable service that receives `AppDbContext` via constructor injection — the same `db` instance the controller already has, which carries the correct tenant context. Update `ServerPortalController` to inject `SharedOrderDraftStore` and remove all `new AppDbContext()` calls from the store.

**Fix Bug #3 — Admin UpdateStatus bypasses CanCashierComplete.**
In `AdminOrdersController.UpdateStatus`, before calling `_ops.UpdateOrderStatus(orderId, "Completed", ...)`, add a guard that reads the current order status and calls `OrderWorkflow.CanCashierComplete(order.Status, order.OrderOrigin)`. If it returns false, return a `BadRequest` with a clear message. This makes the admin path consistent with the cashier path and prevents marking a `Waiting` order as completed.

**Fix Bug #4 — Admin append resets to "In Kitchen" instead of "PendingCashier".**
In `AdminOrdersController.ApplyOpenCheckAppendMutations`, the block that resets status when items are added to a `Ready` or `Served` order sets `existing.Status = "In Kitchen"`. Change this to `existing.Status = OrderWorkflow.PendingCashier`. This makes the admin and server paths consistent: both reset to cashier queue, both require a cashier release before going back to kitchen.

**Fix Bug #5 — Ledger revenue computed with wrong tax rate.**
In `FinancialTransactionService.RecordCompletedOrderRevenue`, the `ComputeTotals` call recomputes the grand total from scratch using `SettingsManager.Load()` (local file). Instead, use `order.PaymentAmountUsd` directly as the merchandise revenue base — it was computed correctly at order creation time using the cloud pricing. The delivery fee split is already stored in `order.DeliveryFeeUsd`. Stop recomputing; read what was stored.

**Fix Bug #6 — TryCompleteOrderOnAccount re-fetches live product prices.**
Replace `ComputeOrderGrandTotalUsd(order)` with `order.PaymentAmountUsd` as the debt amount. The value is already correct and stored. This is a one-line change.

**Add a global exception handler middleware.**
Register `app.UseExceptionHandler(...)` in `Program.cs` before the other middleware. It should catch any unhandled exception, log it via Serilog with the correlation ID, and return a standardized `500` JSON response: `{ "error": "An unexpected error occurred", "correlationId": "..." }`. This ensures no stack trace ever leaks to a browser in production and every error is logged with a traceable ID.

**Restrict the cancel endpoint to Cashier/Admin roles.**
`POST /api/staff/orders/{id}/cancel` uses `[Authorize(Policy = "StaffAny")]` which allows any authenticated staff including Kitchen and Bar. Change this to `[Authorize(Policy = "CashierDesk")]` (Admin, Manager, Cashier) or create a new `"CancelOrder"` policy that includes Cashier, Admin, and Manager but excludes Chef, Barman, and Server. Kitchen and bar staff should not be able to cancel tickets — that is the cashier's responsibility.

---

## 3. Database and Storage — 68 → 75

### Why it scored 68
PostgreSQL with EF Core is solid. Brought down by: images stored as `byte[]` in the database, migrations running on startup (risky), and no connection pool tuning.

### What 75 means here
The database doesn't serve binary image files. Migrations run in a controlled way. The connection pool is sized for your server.

### What to change

**Move images to DigitalOcean Spaces (object storage).**
Currently `PublicMenuAsset.Content` is a `byte[]` column in PostgreSQL. Every time a staff member loads the guest menu or a portal that displays a product photo, the API fetches that binary from the database and streams it to the browser. On a busy Friday night with 20 guests scanning QR codes simultaneously, this means 20 concurrent binary reads from your DB for images that never change. DigitalOcean Spaces is S3-compatible object storage that costs ~$5/month for 250 GB. The migration path: add a `ContentUrl` column to `PublicMenuAsset`. When the desktop pushes a new image, upload it to Spaces via the S3 API and store the resulting public URL. Change image endpoints to redirect to the Spaces URL instead of streaming bytes. Keep the `Content` column for backward compatibility during the transition, then deprecate it. The Spaces CDN (included with Spaces) then serves the images from an edge node instead of your API container.

**Separate migrations from application startup.**
`DatabaseInitializer.Initialize()` is called in `Program.cs` before the app starts. If a migration fails — bad SQL, schema conflict, connection timeout — the app refuses to start and you have an outage. The correct pattern is to run migrations as a separate step in your deploy pipeline before the new container starts. In your GitHub Actions workflow, add a step that runs `dotnet ef database update` using the production connection string (stored as a GitHub secret) after the build but before the DigitalOcean deploy step. Remove `DatabaseInitializer.Initialize()` from `Program.cs` startup. This means a failing migration stops the deploy, not the running app.

**Configure Npgsql connection pool explicitly.**
Npgsql uses a default pool size of 100 connections. For a `basic-xxs` instance with 0.5 vCPU, 100 connections is excessive and can exhaust PostgreSQL's `max_connections` on the managed DB tier. In your `UseNpgsql` call, append `Maximum Pool Size=25;Minimum Pool Size=2` to the connection string, or use the Npgsql data source builder to set `MaxPoolSize = 25`. For a local restaurant serving ~20 concurrent tablet users, 25 connections is more than enough and keeps your managed DB from being overwhelmed. Also add `Connection Idle Lifetime=300` to recycle idle connections every 5 minutes.

---

## 4. Scaling — 18 → 75

### Why it scored 18
SignalR uses in-memory backplane, images are in the DB, `SettingsManager.Load()` reads from disk on every request, and there is one instance.

### What 75 means for a local restaurant
You can run two instances. If one dies at 8pm on a Friday, the second one keeps the service running. Staff tablets reconnect automatically. This is not Netflix-scale — it's "don't have a single point of failure."

### What to change

**Add a Redis backplane to SignalR.**
This is the single most important change in the entire document. SignalR's in-memory backplane means all connected clients must be on the same server process. The moment you add a second instance, a kitchen tablet on instance A won't receive events from an order submitted from instance B. DigitalOcean Managed Redis costs $15/month on the smallest tier. The code change is minimal: add the `Microsoft.AspNetCore.SignalR.StackExchangeRedis` NuGet package, then in `Program.cs` change `builder.Services.AddSignalR()` to `builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString)`. That's it. SignalR will route all hub messages through Redis, making multi-instance deployment transparent.

**Cache `SettingsManager.Load()` calls in memory.**
`SettingsManager.Load()` reads from disk on every call. It is called on every request to `/api/server/config`, `/api/cashier/alerts`, every ticket PDF, and many other endpoints. Register a singleton `IMemoryCache` (already a standard ASP.NET Core service). In `SettingsManager.Load()`, check the cache first with a key like `"app-settings"` and a sliding expiration of 60 seconds. Only read from disk on a cache miss. This eliminates hundreds of disk reads per hour during service. When settings are saved (via `SettingsManager.Save()`), invalidate the cache entry.

**Cache `PublicMenuSettings` (cloud settings).**
Similarly, `db.PublicMenuSettings.AsNoTracking().FirstOrDefault(s => s.Key == "default")` is called on almost every controller method — config, tickets, timezone resolution, pricing. This is a DB round-trip for data that changes only when an admin pushes new appearance settings (rare). Register `IMemoryCache` and cache the `PublicMenuSetting` row for 5 minutes with a key like `"public-menu-settings:default"`. Invalidate the cache entry in `AdminSettingsController` whenever the settings are saved. This alone removes dozens of DB queries per request.

**Bump the instance count to 2 after Redis is added.**
In `.do/app.yaml`, change `instance_count: 1` to `instance_count: 2` and `instance_size_slug` from `basic-xxs` to `basic-xs` (1 GB RAM, 1 vCPU). With Redis handling the SignalR backplane, both instances share the real-time message bus. DigitalOcean App Platform's load balancer distributes HTTP traffic between them round-robin. If one instance restarts during a deploy, the other keeps serving. Total additional cost: ~$12/month for the second `basic-xs` instance.

---

## 5. Auth and Permissions — 57 → 75

### Why it scored 57
JWT is solid but CORS is `AllowAnyOrigin`, there is no rate limiting on login, there are no refresh tokens, and role boundaries let Kitchen staff cancel orders.

### What 75 means here
Only your known domains can make API calls. A bot cannot brute-force PINs. Staff tablets don't get logged out mid-service.

### What to change

**Fix CORS to whitelist specific origins.**
In `Program.cs`, replace the `AllowAnyOrigin()` CORS policy with an explicit list:
```
policy.WithOrigins(
    "https://yourdomain.com",
    "https://starfish-app-owtoz.ondigitalocean.app",
    "http://localhost:8080"  // dev only, add env check
)
.AllowAnyHeader()
.AllowAnyMethod()
.AllowCredentials()
```
Read the allowed origins from configuration so you can set them per environment. For SignalR WebSocket connections, CORS must allow credentials. `AllowAnyOrigin` cannot be combined with `AllowCredentials` in ASP.NET Core — which is another reason the current setup is problematic.

**Add rate limiting on the login endpoint.**
`POST /api/auth/login` currently has no rate limit. A 4-digit PIN has 10,000 combinations; a 6-digit PIN has 1,000,000. Without a rate limit, an automated script can try all combinations. Add a new rate limit policy called `"AuthLogin"` in your `AddRateLimiter` block: use a fixed window of 10 requests per minute per IP, with a queue limit of 0. Apply `[EnableRateLimiting("AuthLogin")]` to the `Login` action in `AuthController`. For a restaurant context, 10 login attempts per minute per IP is generous for legitimate use and prohibitive for brute force.

**Extend JWT lifetime to match a service shift.**
Your JWT expires in `ExpirationHours` (configured in `JwtOptions`). If this is set to 1 or 2 hours, a server who logs in at 6pm gets kicked off at 8pm mid-service. Set `ExpirationHours` to 12 or 16 hours for staff tokens — a full service shift plus buffer. For the desktop admin app, the longer token is appropriate. This is not a security regression in a restaurant context where staff are on premises on a trusted network.

**Fix role boundary on cancel endpoint.**
As described in the API section: change `POST /api/staff/orders/{id}/cancel` from `StaffAny` to `CashierDesk` policy. Kitchen and Bar staff should not be able to cancel orders.

---

## 6. Hosting and Deployment — 55 → 75

### Why it scored 55
Clean Dockerfile, working CI, but migrations run on startup, there is no staging environment, and `basic-xxs` is undersized.

### What 75 means here
A bad deploy does not take down the live service. There is a place to test changes before they hit the restaurant.

### What to change

**Add a staging environment.**
In `.do/app.yaml` or the DigitalOcean App Platform console, create a second app called `elite-restaurant-staging` pointing to the same GitHub repo but on the `develop` branch (not `main`). This staging app uses its own PostgreSQL database (a free DigitalOcean dev DB is available). In your GitHub Actions workflow, add a rule: pushes to `develop` deploy to staging, pushes to `main` deploy to production. Before merging to `main`, the developer (you) manually tests the change on staging. This prevents a bug from going directly to the restaurant's live service. Cost: the staging DB on the smallest tier is free or ~$7/month.

**Move migrations out of startup.**
As described in the Database section, add a `dotnet ef database update` step in your GitHub Actions `deploy-production` job that runs against the production connection string (stored as `PROD_DB_CONNECTION` in GitHub Secrets). This step runs before DigitalOcean starts the new container. If the migration fails, the GitHub Action fails, the old container keeps running, and the restaurant never sees an outage. The critical change in `Program.cs`: remove the `DatabaseInitializer.Initialize()` call or guard it with a feature flag so it only runs in development (`if (app.Environment.IsDevelopment())`).

**Upgrade to `basic-xs` for production.**
`basic-xxs` is 512 MB RAM and 0.5 vCPU. A busy Friday night with 10 staff tablets + 20 guest QR sessions + SignalR connections + PDF generation is likely to hit memory pressure on 512 MB. `basic-xs` is 1 GB RAM and 1 vCPU for about $12/month. With two instances (as recommended in the Scaling section), you have 2 GB RAM across the cluster, which is more than sufficient for a single restaurant. Upgrade this in the `.do/app.yaml` `instance_size_slug` field.

---

## 7. Cloud and Compute — 38 → 75

### Why it scored 38
DigitalOcean App Platform is fine but images are in the DB (no CDN), no Redis, and `basic-xxs` is the smallest tier.

### What 75 means here
Images load fast for guests. The API process doesn't waste memory and DB connections serving binary files.

### What to change

**Add DigitalOcean Spaces for image storage.**
DigitalOcean Spaces is S3-compatible object storage with a built-in CDN. Create a Space called `elite-restaurant-assets` in the same region as your app (NYC). Enable the CDN endpoint — DigitalOcean provides a free CDN subdomain (`your-space.nyc3.cdn.digitaloceanspaces.com`). The migration plan:

1. Add `SPACES_KEY`, `SPACES_SECRET`, `SPACES_BUCKET`, `SPACES_REGION` as environment variables in your App Platform configuration.
2. In `AdminSettingsController` (or wherever image uploads are processed), use the AWS SDK for .NET (which is S3-compatible) to upload the image bytes to Spaces and record the resulting public CDN URL.
3. Add a `ContentUrl` string column to `PublicMenuAsset` via an EF migration.
4. Update image-serving endpoints (`/api/public/menu/assets/product/{id}`, `/api/server/assets/restaurant-logo`, etc.) to redirect to the `ContentUrl` if it is set, falling back to the old `Content` byte array for legacy records.
5. Over time, re-upload existing images through the admin and the `Content` column becomes empty.

The CDN serves images from edge nodes close to users. Your API process no longer touches image data. The DB is no longer storing megabytes of binary per product.

**Add DigitalOcean Managed Redis.**
As described in the Scaling section, the $15/month Redis instance unlocks horizontal scaling and is the prerequisite for running two app instances. In the App Platform, add a Redis database component to your app spec. This automatically injects a `REDIS_URL` environment variable into your containers. Use that in `Program.cs` when configuring the SignalR Redis backplane and for the application-level `IMemoryCache` (for settings caching).

---

## 8. CI/CD & Version Control — 58 → 75

### Why it scored 58
Working CI on two platforms, coverage gating, auto-deploy. Brought down by: 20% coverage threshold, no staging deploy step, no secrets scanning, no smoke test after deploy.

### What 75 means here
Bad code does not reach production without failing a gate. A security vulnerability in a dependency gets flagged automatically. You know within 2 minutes of a deploy whether the API is still responding.

### What to change

**Add Dependabot for dependency security scanning.**
Create `.github/dependabot.yml` with entries for both `nuget` (the .NET packages) and `npm` (the elite-menu React packages). Set `schedule.interval: weekly`. Dependabot will automatically open pull requests when a package has a published security vulnerability. This is completely free and requires no configuration beyond the YAML file. For a production app, knowing when `System.Text.Json` or a SignalR dependency has a CVE is important.

**Raise the coverage threshold to 35%.**
The current CI gate is 20%. The workflow comment already acknowledges 40% as the milestone. Move the gate to 35% now. The test suite already has meaningful tests for the domain logic. The areas with low coverage that matter most for a restaurant are: the order workflow state machine, inventory deduction paths, and the financial ledger. Write tests specifically for the 6 bugs that were found — a regression test for each bug is more valuable than testing utility code.

**Add a deploy smoke test job.**
After the DigitalOcean deploy step in your GitHub Actions workflow, add a final job that waits 30 seconds for the new container to start, then sends an HTTP GET to `https://your-production-url/api/health` and asserts it returns `200`. If it doesn't, the CI job fails and you get a notification. This catches "the deploy succeeded but the app crashed on startup" — which is the most common production failure after a migration or configuration error. The smoke test is 5 lines of `curl` in a workflow step.

**Add a staging deploy step.**
As described in the Hosting section, add a `deploy-staging` job in your workflow that triggers on push to `develop` and calls the DigitalOcean API to trigger a deploy on the staging app. The `main` branch continues to trigger the production deploy. This gives you a complete promotion pipeline: `develop` → staging (automatic) → `main` → production (automatic after tests pass).

---

## 9. Security & RLS — 50 → 75

### Why it scored 50
Good security headers and rate limiting on public endpoints, but CORS is wildcard, `/api/health/db` is public, CSP uses `'unsafe-inline'`, and no brute-force protection on login.

### What 75 means here
No unexpected parties can call your API. Admin-level information is not publicly visible. The most common web attacks (XSS, clickjacking, MIME sniffing) are blocked.

### What to change

**Fix CORS (same as Auth section — highest priority).**
Already covered above. This is the most impactful single security change.

**Protect `/api/health/db`.**
The `/api/health` endpoint (public, returns `200 ok`) is fine. But `/api/health/db` returns entity counts: number of employees, orders, tables, products. This is information disclosure — a competitor or attacker can see that your restaurant has 8 employees, 47 orders today, etc. Add `[Authorize(Policy = "AdminRead")]` to the `GetDb()` method in `HealthController`. Keep `Get()` public for the DigitalOcean health check.

**Add HSTS header.**
Call `app.UseHsts()` in `Program.cs` when `!app.Environment.IsDevelopment()`. Add `builder.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); })`. HSTS tells browsers to always use HTTPS for your domain and refuse HTTP connections — important now that you have a production domain.

**Tighten the Content Security Policy.**
The current CSP includes `'unsafe-inline'` for both `script-src` and `style-src`. `'unsafe-inline'` for scripts negates the XSS protection that CSP provides. The portals use inline `<script>` blocks heavily, so removing `'unsafe-inline'` requires moving scripts to external `.js` files and adding a `nonce` attribute to remaining inline scripts. For a pragmatic 70-score improvement without a full refactor: at minimum, remove `'unsafe-inline'` from `style-src` (inline styles are less dangerous but still bad practice) and consider adding `'nonce-{random}'` to `script-src` using ASP.NET Core's built-in nonce support, which allows whitelisted inline scripts without the wildcard.

**Protect the setup wipe endpoint more robustly.**
`POST /api/setup/wipe` uses an `X-Setup-Secret` header. The secret is a string in configuration. This is acceptable but add a secondary guard: only allow this endpoint when `ASPNETCORE_ENVIRONMENT` is not `Production`, or add an IP allowlist check so only requests from your own machine (or a specific admin IP) can reach it. A misconfigured secret or a leaked environment variable should not be the only thing between the internet and a database wipe.

---

## 10. Rate Limiting — 43 → 75

### Why it scored 43
Rate limiting exists only on 3 public routes. The auth endpoint (brute-force risk), all staff endpoints, and SignalR have no limiting.

### What 75 means here
A bot cannot brute-force PINs. A misbehaving client cannot bring down the API by hammering staff endpoints.

### What to change

**Add rate limiting to the auth login endpoint.**
This is the most critical gap. Add a `"AuthLogin"` policy in `AddRateLimiter`: 10 requests per minute per IP using a fixed window. Apply it to `POST /api/auth/login`. Also consider a second partition by `staffId` from the request body — this prevents an attacker who knows a staff ID from brute-forcing the PIN even across multiple IPs.

**Add a global default rate limit.**
In `AddRateLimiter`, set `options.GlobalLimiter` to a `PartitionedRateLimiter` with a permissive but protective default: 300 requests per minute per IP. This applies to every endpoint that doesn't have a more specific policy. For staff tablets making ~1 request per second (polling + actions), 300/minute is comfortable. For a bot, it is a meaningful constraint. The global limiter is a safety net, not a primary defense.

**Switch from fixed window to sliding window for public routes.**
Fixed window allows a burst: 60 requests in the last second of one window plus 60 in the first second of the next = 120 requests in 2 seconds. A sliding window prevents this by distributing the limit across a rolling time frame. Change `GetFixedWindowLimiter` to `GetSlidingWindowLimiter` in the `PublicMenuRead` and `PublicMenuDraft` policies. The parameters are identical; only the class name changes.

**Handle the proxy IP issue.**
Your rate limiter partitions by `context.Connection.RemoteIpAddress`. Behind DigitalOcean's load balancer and App Platform proxy, `RemoteIpAddress` may be the proxy's IP, not the client's — which means all guests share one rate limit bucket. The fix: read `X-Forwarded-For` from the request header instead. In the `GetPartitionKey` function in `Program.cs`, change it to: read `context.Request.Headers["X-Forwarded-For"].FirstOrDefault()` as the primary key, falling back to `RemoteIpAddress`. You already have `app.UseForwardedHeaders()` configured, which means `context.Connection.RemoteIpAddress` should already be the real client IP after that middleware runs — verify this is the case by checking that `ForwardedHeadersOptions` is configured before the rate limiter middleware in the pipeline order.

---

## 11. Load Balancing and Scaling — 15 → 75

### Why it scored 15
One instance. SignalR in-memory backplane. No horizontal scaling possible.

### What 75 means here
You can run two instances. If one crashes during service, the other serves normally. Staff don't notice.

### What to change

**Add Redis backplane (same as Scaling section — this is the root cause).**
Everything in this category resolves from a single change: add the Redis SignalR backplane. Once Redis is the message broker for SignalR, adding a second instance is a configuration change (`instance_count: 2`), not an engineering project. The DigitalOcean load balancer in App Platform distributes HTTP requests between instances automatically using round-robin. No additional load balancer configuration is needed at this scale.

**Understand what cannot be horizontally scaled yet (and accept it).**
`SettingsManager` reads from a file on disk. In a multi-instance deployment, each instance has its own container filesystem. If one instance updates `app-settings.json`, the other instance doesn't see the change until its cache expires. The fix is the `IMemoryCache` caching described in the Scaling section — once settings are cached in memory and only refreshed from the DB (`PublicMenuSettings`), the file is only read once per restart and settings sync through the database instead. This makes the app stateless enough for two instances to serve the same restaurant without diverging.

**Configure sticky sessions for SignalR WebSocket connections.**
Even with a Redis backplane, SignalR prefers to keep a WebSocket connection on the same server for the duration of a session (the backplane is used for cross-server message routing, not connection persistence). In DigitalOcean App Platform's load balancer settings, enable session persistence (sticky sessions) using the `X-Forwarded-For` header or cookie affinity. This is a configuration toggle in the App Platform UI under the service's networking settings. It ensures a tablet connected to instance A stays on instance A for the life of the WebSocket connection, using Redis only when an event originates from instance B.

---

## 12. Error Tracking and Logs — 44 → 75

### Why it scored 44
Serilog is wired correctly, correlation IDs exist, but logs write to the container's ephemeral filesystem (lost on every deploy/restart), there is no external log aggregation, no error tracking, and no alerting.

### What 75 means here
When the API breaks at 8pm on Friday, you find out immediately (not when the restaurant calls you). You can look up the error with a correlation ID in a searchable log dashboard. Logs survive restarts.

### What to change

**Add Sentry for error tracking.**
Sentry has a free tier (5,000 errors/month) that is more than enough for a single restaurant. Install `Sentry.AspNetCore` NuGet package. In `Program.cs`, add `builder.WebHost.UseSentry(o => { o.Dsn = builder.Configuration["Sentry:Dsn"]; o.Environment = app.Environment.EnvironmentName; })`. Store the DSN as an environment variable in App Platform (`SENTRY__DSN`). From that point forward, every unhandled exception is automatically captured and sent to the Sentry dashboard with the full stack trace, request context, and correlation ID. You get an email alert on the first occurrence of any new error. This is a 15-minute setup and is the highest-ROI change in this entire document for operational reliability.

**Ship logs to an external sink (Logtail or DigitalOcean's built-in logging).**
DigitalOcean App Platform streams container logs to its built-in log viewer (available in the App Platform console). This is already functional without any code change — you just need to check the App Platform UI. For longer retention and search, add the `Serilog.Sinks.Logtail` NuGet package. Logtail's free tier retains logs for 3 days; paid plans start at $10/month. In your Serilog bootstrap configuration, add `.WriteTo.Logtail(sourceToken: Configuration["Logtail:Token"])`. Alternatively, if you want zero cost and acceptable search capability, add `.WriteTo.Console()` only (remove the `.WriteTo.File()` sink) and rely entirely on DigitalOcean's log streaming — console output in App Platform is automatically captured and accessible in the UI for the last 24 hours.

**Add uptime monitoring.**
UptimeRobot is free for up to 50 monitors with 5-minute check intervals. Create a monitor for `https://your-production-url/api/health`. Configure it to send an email (and optionally an SMS) when the endpoint is down for more than 5 minutes. This means you find out the restaurant's system is down within 5 minutes, not when someone calls you. Setup takes 3 minutes on uptimerobot.com.

**Add structured logging for key business events.**
Your existing Serilog request logging records every HTTP request. Add explicit structured log events for the most operationally important actions: order released to kitchen, order completed with payment, debt settlement, staff login failures. Use `Log.Information("Order {OrderId} released to kitchen by {CashierId}", orderId, session.EmployeeId)` so these events are easily searchable and filterable in Logtail or Sentry. This turns your logs from "HTTP 200 in 45ms" into an audit trail you can actually use when something goes wrong.

---

## 13. Availability and Recovery — 35 → 75

### Why it scored 35
Single instance, RPO of 24 hours, manual backup script on Windows Task Scheduler, migrations run on startup, no WAL archiving.

### What 75 means here
A disk failure does not lose more than a few hours of data. A bad deploy does not cause an outage. If the single container crashes, it auto-restarts within 30 seconds. You have tested a restore at least once.

### What to change

**Enable automated backups on DigitalOcean Managed PostgreSQL.**
If you are using DigitalOcean Managed PostgreSQL (as the deployment docs indicate), automated daily backups are already included and enabled by default. Go to your database cluster in the DigitalOcean control panel → Backups tab. Verify daily backups are listed. The free retention period is 7 days. This alone reduces your RPO from "whatever the last manual pg_dump was" to "yesterday at 2am at worst." This requires no code change — just verify the setting is on.

**Enable Point-in-Time Recovery (PITR).**
DigitalOcean Managed PostgreSQL supports PITR through WAL archiving at the $15/month tier and above. PITR lets you restore to any moment in the last 7 days, not just the daily snapshot. For a restaurant where an accidental `wipe-all-tenant-data.sql` run at the wrong time is a real risk (you have this script in your `scripts/` directory), PITR is the difference between "we lost 8 hours of orders" and "we lost 3 minutes of orders." Enable it in the DB cluster settings in the DigitalOcean control panel. No application code changes needed.

**Move migrations out of startup (recovery impact).**
As described in the Hosting section, separating migrations from startup means a failed migration during a deploy does not take down the running app. The old container keeps serving. This directly improves availability: the failure mode changes from "site is down" to "deploy failed — old version still running."

**Document and test a restore procedure.**
Write a runbook (a single markdown file: `docs/RESTORE-RUNBOOK.md`) with the exact steps to restore from a DigitalOcean managed backup: how to create a new DB cluster from a backup snapshot, how to point the App Platform environment variable to the new DB, and how to verify the restore was successful (check order count, last order date). Run through this procedure at least once on a copy of the production database before you need it under pressure at 9pm on a Saturday. The BACKUP-AND-RECOVERY.md doc already exists and is good — add the DigitalOcean-specific steps for managed DB restore.

**Confirm auto-restart is configured.**
DigitalOcean App Platform automatically restarts crashed containers — this is built into the platform. Verify your app's health check (`/api/health`) is correctly configured in `.do/app.yaml` (`http_path: /api/health`). If the health check fails three times in a row, App Platform restarts the container automatically. This gives you ~30 seconds of downtime on a crash vs. "down until someone notices." Already present in your config — just confirm it is working by checking the App Platform deployment logs.

**Replace the Windows Task Scheduler backup script with DigitalOcean Managed backup.**
The `scripts/backup-postgres.bat` script requires a Windows machine to be running at 2am, connected, with `pg_dump` installed. If the machine is off, asleep, or disconnected, the backup doesn't run. DigitalOcean Managed PostgreSQL's automated backups run server-side regardless of your local machine state. Once you verify managed backups are enabled (step 1 above), the manual script is no longer your primary backup mechanism. Keep it as a secondary option for on-demand snapshots before risky operations (like running SeedRunner), but don't rely on it as your daily backup.

---

## Implementation Priority Order

For a local restaurant that wants to reach 70-75 across all categories without a long project, do these in order:

| Priority | Action | Time | Cost/month |
|---|---|---|---|
| 1 | Enable DigitalOcean managed DB backups + verify PITR | 10 min | Included |
| 2 | Add Sentry free tier | 15 min | Free |
| 3 | Add UptimeRobot monitor | 5 min | Free |
| 4 | Fix CORS to whitelist origins | 20 min | Free |
| 5 | Add rate limit on /api/auth/login | 20 min | Free |
| 6 | Move migrations out of startup | 30 min | Free |
| 7 | Fix the 6 critical bugs (see API section) | 2–4 hours | Free |
| 8 | Add Redis (DigitalOcean Managed) | 1 hour | $15 |
| 9 | Add Dependabot + raise coverage to 35% | 30 min | Free |
| 10 | Cache SettingsManager + PublicMenuSettings | 1 hour | Free |
| 11 | Bump to basic-xs, instance_count: 2 | 10 min | +$12 |
| 12 | Migrate images to DigitalOcean Spaces | 2–3 hours | $5 |
| 13 | Add staging environment | 30 min | $7 |
| 14 | Fix portal UX (cancel flow, error states, SignalR reconnect) | 2 hours | Free |
| 15 | Add smoke test in CI | 30 min | Free |
| 16 | Protect /api/health/db, add HSTS | 20 min | Free |

**Total additional infrastructure cost: ~$39/month**
(Redis $15 + second instance $12 + Spaces $5 + staging DB $7)

That is the cost of a production-grade local restaurant system. Everything on the free tier list costs only engineering time.

---

## Score Projection After All Changes

| Category | Before | After |
|---|---|---|
| Frontend | 62 | 74 |
| API & Backend Logic | 65 | 80 |
| Database and Storage | 68 | 78 |
| Scaling | 18 | 75 |
| Auth and Permissions | 57 | 76 |
| Hosting and Deployment | 55 | 76 |
| Cloud and Compute | 38 | 74 |
| CI/CD & Version Control | 58 | 76 |
| Security & RLS | 50 | 74 |
| Rate Limiting | 43 | 75 |
| Load Balancing and Scaling | 15 | 75 |
| Error Tracking and Logs | 44 | 76 |
| Availability and Recovery | 35 | 76 |
| **Overall Average** | **47** | **76** |

---

*Generated for EliteRestaurant — upgrade guide from current state to 70–75/100 production readiness across all categories.*
