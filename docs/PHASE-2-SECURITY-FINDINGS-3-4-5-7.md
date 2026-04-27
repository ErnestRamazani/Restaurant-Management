# Phase 2 — Security (Findings 3, 4, 5, and 7)

This document records **what was wrong**, **what we implemented**, **where the code lives**, and **how to operate** the system after these changes. It corresponds to the “security” phase addressing **Finding 3** (PIN storage), **Finding 4** (CORS and token-in-URL leaks), **Finding 5** (database credentials on disk and in UI), and **Finding 7** (HTTPS on LAN).

---

## Table of contents

1. [Finding 3 — Staff PINs: hashin6g, verification, and admin UX](#finding-3--staff-pins-hashing-verification-and-admin-ux)
2. [Finding 4 — CORS truthfulness and Bearer-only asset URLs](#finding-4--cors-truthfulness-and-bearer-only-asset-urls)
3. [Finding 5 — PostgreSQL credentials: DPAPI, structured settings, environment variables](#finding-5--postgresql-credentials-dpapi-structured-settings-environment-variables)
4. [Finding 7 — HTTPS on the LAN (self-signed PFX)](#finding-7--https-on-the-lan-self-signed-pfx)
5. [Cross-reference: new or notable files](#cross-reference-new-or-notable-files)
6. [Environment variables and configuration keys](#environment-variables-and-configuration-keys)
7. [Operational checklist after deployment](#operational-checklist-after-deployment)

---

## Finding 3 — Staff PINs: hashing, verification, and admin UX

### Original risk

- Employee **PINs** were effectively treated as **plaintext** in the database and in several code paths.
- **Tablet / API login** compared PINs in ways that assumed readable storage.
- **Admin UI** could expose or encourage copying full credential material in clear text.

### Design goals

1. Persist only a **one-way hash** of the PIN (BCrypt), never the raw PIN in normal operation.
2. **Verify** logins against the hash (with a temporary **legacy plaintext** path only where needed for migration).
3. **Hash on save** in HR/admin flows and in seed/bootstrap data.
4. Keep **PIN entry** in the WPF app on controls that do not echo secrets (e.g. `PasswordBox`).

### Core abstraction: `EmployeePinHasher`

**File:** `EliteRestaurant.Core/Utils/EmployeePinHasher.cs`

Responsibilities:

- **`HashForStorage`** — produces a BCrypt string for `Employee.PinCode`.
- **`Verify`** — checks a user-entered PIN against either a BCrypt hash or a **legacy** stored value (plaintext) for backward compatibility during migration.
- **`LooksLikeBcryptHash`** — detects `$2…` BCrypt format.

```7:35:EliteRestaurant.Core/Utils/EmployeePinHasher.cs
public static class EmployeePinHasher
{
    public static string HashForStorage(string plainPin)
    {
        var p = (plainPin ?? string.Empty).Trim();
        if (p.Length == 0)
            throw new ArgumentException("PIN cannot be empty.", nameof(plainPin));
        return BCrypt.Net.BCrypt.HashPassword(p);
    }

    /// <summary>True if <paramref name="plainPin"/> matches the stored BCrypt hash or legacy plaintext.</summary>
    public static bool Verify(string plainPin, string? storedHashOrLegacy)
    {
        if (string.IsNullOrWhiteSpace(plainPin) || string.IsNullOrWhiteSpace(storedHashOrLegacy))
            return false;
        var pin = plainPin.Trim();
        var stored = storedHashOrLegacy.Trim();
        if (LooksLikeBcryptHash(stored))
            return BCrypt.Net.BCrypt.Verify(pin, stored);
        return string.Equals(pin, stored, StringComparison.Ordinal);
    }

    public static bool LooksLikeBcryptHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 59)
            return false;
        return value.StartsWith("$2", StringComparison.Ordinal);
    }
}
```

**Package:** `BCrypt.Net-Next` is referenced from `EliteRestaurant.Core/EliteRestaurant.Core.csproj`.

### Model documentation

**File:** `EliteRestaurant.Core/Models/Employee.cs`

`PinCode` is documented as storing a **BCrypt hash**, not a plaintext PIN.

### Where verification happens

| Location | Role |
|----------|------|
| `EliteRestaurant.Api/Security/TabletAuthService.cs` | API tablet login: filters candidates with `EmployeePinHasher.Verify(normalizedPin, e.PinCode)`. |
| `EliteRestaurantPro/ViewModels/StaffLoginViewModel.cs` | WPF staff login: same verification pattern against EF-loaded employees. |
| `EliteRestaurantPro/ViewModels/EmployeesViewModel.cs` | Admin save: **hashes** new/changed PINs with `HashForStorage`; duplicate-PIN check uses `Verify` so it works for hashes. |

Example (API):

```21:30:EliteRestaurant.Api/Security/TabletAuthService.cs
        using var db = new AppDbContext();
        var candidates = db.Employees.AsNoTracking()
            .Where(e => e.EmploymentStatus == "Active")
            .Where(e =>
                (!string.IsNullOrWhiteSpace(e.SignInId) &&
                 e.SignInId.Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
                || (e.UniqueId ?? string.Empty).Trim().Equals(id, StringComparison.OrdinalIgnoreCase))
            .AsEnumerable()
            .Where(e => EmployeePinHasher.Verify(normalizedPin, e.PinCode))
            .ToList();
```

### Bootstrap / sample data

**File:** `EliteRestaurant.Core/Data/AppDbContext.cs` (and related seed paths)

Seeded employees use `EmployeePinHasher.HashForStorage("…")` for PINs instead of raw strings (see grep hits around the sample `Employee` list).

**File:** `SeedRunner/Program.cs` — uses `EmployeePinHasher.HashForStorage(pin)` when creating employees.

### Admin UI (WPF)

**File:** `EliteRestaurantPro/Views/EmployeesView.xaml` + code-behind

- PIN is captured from a **`PasswordBox`** (`PasswordChanged` handler pushes into the view model), not a visible `TextBox` bound to a cleartext string for display.

**File:** `EliteRestaurantPro/ViewModels/EmployeesViewModel.cs`

- On add/edit, PINs are normalized, validated for role requirements, hashed with `HashForStorage` when saving to the database.

### Important limitation (by design)

- **Administrators cannot “recover”** original PINs from the database; only **reset** them by setting a new PIN and saving. This is inherent to one-way hashing.

---

## Finding 4 — CORS truthfulness and Bearer-only asset URLs

### Original risk

1. **CORS policy** was named as if it restricted LAN callers but used **`AllowAnyOrigin()`**, which allows **any** website to call the API from a browser context (subject to CORS rules), undermining the stated security posture.
2. **Session tokens** were appended to **image URLs** as `?token=…`, leaking tokens into logs, history, referrers, and screenshots.

### CORS: configured origins only

**File:** `EliteRestaurant.Api/Program.cs`

- Policy name: **`RestrictToConfiguredOrigins`** (honest name).
- Origins come from **`Cors:AllowedOrigins`** in configuration (array of strings).
- If the array is **empty**, the host **fails at startup** with an explicit exception (fail-closed).
- Policy uses **`WithOrigins(...)`** plus `AllowAnyHeader` / `AllowAnyMethod` (no `AllowAnyOrigin`).

CORS validation and policy registration (same file also configures LAN HTTPS—see Finding 7):

```14:19:EliteRestaurant.Api/Program.cs
const string CorsPolicyRestrictToConfiguredOrigins = "RestrictToConfiguredOrigins";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? Array.Empty<string>();
if (corsOrigins.Length == 0)
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must list at least one origin (e.g. https://192.168.x.x:7194 for tablets).");
```

```66:71:EliteRestaurant.Api/Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyRestrictToConfiguredOrigins, policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
```

**File:** `EliteRestaurant.Api/appsettings.json` — default list includes local HTTP/HTTPS dev origins; **operators must add** each tablet/browser origin (scheme + host + port), e.g. `https://192.168.1.50:7194`.

### Assets: no token in query string

**File:** `EliteRestaurant.Api/Controllers/ServerPortalController.cs`

- `GET /api/server/config` returns **path-only** URLs: `/api/server/assets/restaurant-logo` and `/api/server/assets/me-photo` (no `token` query parameter).
- `GET` handlers for those assets take **no** `[FromQuery] token`; they use **`RequireServerSession()`**, which reads **`Authorization: Bearer`** via `HttpRequest.ReadBearerToken()` (see `HttpAuthExtensions`).

```17:64:EliteRestaurant.Api/Controllers/ServerPortalController.cs
    [HttpGet("config")]
    public ActionResult<ServerPortalConfigDto> GetConfig()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized(new { message = "Missing/invalid token or non-server role." });

        var allSettings = SettingsManager.Load();
        var settings = allSettings.CurrencyPricing;
        var business = allSettings.BusinessProfile;
        var logoUrl = "/api/server/assets/restaurant-logo";
        var employeePhotoUrl = "/api/server/assets/me-photo";
        // ...
    }

    [HttpGet("assets/restaurant-logo")]
    public IActionResult GetRestaurantLogo()
    {
        var session = RequireServerSession();
        if (session is null)
            return Unauthorized();
        // ...
    }
```

**File:** `EliteRestaurant.Api/Security/HttpAuthExtensions.cs` — parses the `Bearer` token from the `Authorization` header.

**File:** `EliteRestaurant.Api/wwwroot/index.html`

- The SPA loads images with **`fetch(url, { headers: { Authorization: "Bearer " + token } })`**, then assigns **`URL.createObjectURL(blob)`** to `<img>`, because a plain `img src` cannot send custom headers.

Related client logic appears around the `setAuthImage` helper and `loadPortalData` (see file for full script).

### Related client API calls

The same portal script’s `api()` helper already sends `Authorization: Bearer` for JSON calls when `auth` is true.

---

## Finding 5 — PostgreSQL credentials: DPAPI, structured settings, environment variables

### Original risk

- Full **PostgreSQL connection strings** (including **password**) were stored as **plaintext JSON** under `%LocalAppData%\EliteRestaurantPro\settings\app-settings.json`.
- The **Appearance → Database** screen exposed the **entire connection string** in an editable text box.

### Preferred transport for automation / services

**Environment variables** (already supported in code):

- `ELITE_DB_PROVIDER=PostgreSql`
- `ELITE_POSTGRES_CONNECTION=<full connection string>`

These take **precedence** over file-based settings when resolving the database connection (see `AppDbContext.TryGetPostgreSqlConnectionString` below).

### Structured settings + DPAPI password at rest

**File:** `EliteRestaurant.Core/Utils/AppSettings.cs`

`DatabaseSettings` now contains:

- `PostgreSqlHost`, `PostgreSqlPort`, `PostgreSqlDatabase`, `PostgreSqlUsername`
- `PostgreSqlPasswordProtected` — Base64 encoding of **DPAPI-protected** UTF-8 password bytes (`DataProtectionScope.CurrentUser` on Windows).
- Legacy `PostgreSqlConnectionString` — kept for **deserialization** and migration; omitted from JSON when empty (`JsonIgnore` when default).

```13:28:EliteRestaurant.Core/Utils/AppSettings.cs
public sealed class DatabaseSettings
{
    // Supported value: PostgreSql
    public string Provider { get; set; } = "PostgreSql";

    /// <summary>Legacy plaintext; cleared after migration to structured fields + <see cref="PostgreSqlPasswordProtected"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string PostgreSqlConnectionString { get; set; } = string.Empty;

    public string PostgreSqlHost { get; set; } = string.Empty;
    public int PostgreSqlPort { get; set; } = 5432;
    public string PostgreSqlDatabase { get; set; } = string.Empty;
    public string PostgreSqlUsername { get; set; } = string.Empty;

    /// <summary>Windows DPAPI-protected password (Base64), CurrentUser scope.</summary>
    public string PostgreSqlPasswordProtected { get; set; } = string.Empty;
}
```

**File:** `EliteRestaurant.Core/Utils/DatabaseConnectionSecret.cs`

Wraps `ProtectedData.Protect` / `Unprotect` with UTF-8 text; throws on non-Windows with guidance to use environment variables instead.

**File:** `EliteRestaurant.Core/Utils/DatabaseSettingsResolver.cs`

Builds an `NpgsqlConnectionString` from structured fields + decrypted password, or returns a **legacy** plaintext connection string **only** while `PostgreSqlHost` is still empty (pre-migration).

**File:** `EliteRestaurant.Core/Utils/DatabaseSettingsMigration.cs`

On load, if a legacy `PostgreSqlConnectionString` exists and `PostgreSqlHost` is empty, parses with `NpgsqlConnectionStringBuilder`, fills structured fields, encrypts password with DPAPI, clears legacy string.

**File:** `EliteRestaurant.Core/Utils/SettingsManager.cs`

After deserializing `app-settings.json`, runs migration and **saves** if migration changed data:

```20:24:EliteRestaurant.Core/Utils/SettingsManager.cs
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            loaded.Database ??= new DatabaseSettings();
            if (DatabaseSettingsMigration.TryMigrateInMemory(loaded.Database))
                Save(loaded);
            return loaded;
```

### Connection resolution (public API)

**File:** `EliteRestaurant.Core/Data/AppDbContext.cs`

- **`TryGetPostgreSqlConnectionString`** is **public static** for reuse (e.g. `App.xaml.cs`).
- Order: **environment** first, then **`DatabaseSettingsResolver.TryBuildFromSettings`**.

```417:441:EliteRestaurant.Core/Data/AppDbContext.cs
    public static bool TryGetPostgreSqlConnectionString(out string connectionString)
    {
        connectionString = string.Empty;

        var envProvider = Environment.GetEnvironmentVariable("ELITE_DB_PROVIDER");
        var envConnection = Environment.GetEnvironmentVariable("ELITE_POSTGRES_CONNECTION");
        if (string.Equals(envProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(envConnection))
        {
            connectionString = envConnection.Trim();
            return true;
        }

        DatabaseSettings settings;
        try
        {
            settings = SettingsManager.Load().Database ?? new DatabaseSettings();
        }
        catch
        {
            settings = new DatabaseSettings();
        }

        return DatabaseSettingsResolver.TryBuildFromSettings(settings, out connectionString);
    }
```

**Package:** `System.Security.Cryptography.ProtectedData` in `EliteRestaurant.Core.csproj`.

### WPF UI and first-run bootstrap

**File:** `EliteRestaurantPro/Views/AppearanceSettingsView.xaml` + `AppearanceSettingsView.xaml.cs`

- Replaced single multiline connection string text box with **Host**, **Port**, **Database**, **Username**, and **`PasswordBox`** for password (not displayed as cleartext).
- View model exposes **`HasSavedDatabasePassword`** and **`SetDatabasePasswordFromUi`**, and raises **`NotifyClearDatabasePassword`** after save to clear the password box in memory.

**File:** `EliteRestaurantPro/ViewModels/AppearanceSettingsViewModel.cs`

- **`SaveDatabaseSettings`** / **`TestDatabaseConnection`** / **`BuildConnectionStringForTest`** use structured fields + pending password or DPAPI-unprotected stored password.

**File:** `EliteRestaurantPro/App.xaml.cs`

- First-run and retry dialogs use **pipe-separated** bootstrap: `host|port|database|username|password` (password may contain `|`; everything after field 4 is joined).
- Writes structured settings + DPAPI password; clears legacy connection string field.

### DPAPI scope caveat

- **CurrentUser** scope means the ciphertext is tied to the **Windows user** who saved it. A Windows **service** or another user account running the API may not decrypt the same file—use **`ELITE_POSTGRES_CONNECTION`** for those deployments.

---

## Finding 7 — HTTPS on the LAN (self-signed PFX)

### Original risk

- API defaulted to **HTTP-only** on LAN (`http://0.0.0.0:5223`), so **staff ID + PIN** (and other payloads) crossed Wi‑Fi in **cleartext**.

### Implementation overview

**File:** `EliteRestaurant.Api/Program.cs`

- Reads **`LanHttps`** settings: certificate path (default `certs/elite-lan.pfx` under content root), `HttpPort` (default **5223**), `HttpsPort` (default **7194**).
- Certificate password: **`ELITE_LAN_CERTIFICATE_PASSWORD`** environment variable, then **`LanHttps:CertificatePassword`** in configuration.
- If the **PFX file exists**, Kestrel listens on **HTTPS** on all interfaces (`IPAddress.Any`, HTTPS port) and **HTTP** on the HTTP port; registers **HTTPS redirection** to the HTTPS port.
- If the PFX is **missing**, logs a console warning and serves **HTTP only** on the HTTP port (developer convenience until a cert is exported).

```21:77:EliteRestaurant.Api/Program.cs
var lanSection = builder.Configuration.GetSection("LanHttps");
var httpPort = lanSection.GetValue("HttpPort", 5223);
var httpsPort = lanSection.GetValue("HttpsPort", 7194);
var certRelative = lanSection["CertificatePath"] ?? "certs/elite-lan.pfx";
var certPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, certRelative));
var certPassword = Environment.GetEnvironmentVariable("ELITE_LAN_CERTIFICATE_PASSWORD")
                   ?? lanSection["CertificatePassword"]
                   ?? "";

var lanHttpsEnabled = File.Exists(certPath);
if (lanHttpsEnabled)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.HttpsPort = httpsPort;
    });
}

builder.WebHost.ConfigureKestrel((_, options) =>
{
    if (lanHttpsEnabled)
    {
        try
        {
            var cert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.EphemeralKeySet);
            options.Listen(IPAddress.Any, httpsPort, listen => listen.UseHttps(cert));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load HTTPS certificate '{certPath}'. " +
                "Set ELITE_LAN_CERTIFICATE_PASSWORD if the PFX is password-protected. See docs/HTTPS-LAN.md.",
                ex);
        }
    }
    else
    {
        Console.WriteLine(
            $"[EliteRestaurant.Api] LAN HTTPS certificate not found at '{certPath}'. " +
            $"HTTP only on port {httpPort}. Export a PFX and restart (docs/HTTPS-LAN.md).");
    }

    options.Listen(IPAddress.Any, httpPort);
});
// ...
if (lanHttpsEnabled)
    app.UseHttpsRedirection();
```

**File:** `EliteRestaurant.Api/appsettings.json` — `LanHttps` section and expanded `Cors:AllowedOrigins` for `https://localhost:7194` and `https://127.0.0.1:7194`.

**File:** `EliteRestaurant.Api/Properties/launchSettings.json` — `applicationUrl` removed so Kestrel binding from code/config is authoritative; `launchUrl` points to Swagger on HTTP or HTTPS profile.

**File:** `EliteRestaurant.Api/docs/HTTPS-LAN.md` — operator steps: `dotnet dev-certs https --export-path …`, set password env, trust cert on tablets, add CORS origins.

**File:** `.gitignore` — ignores `EliteRestaurant.Api/certs/*.pfx` and `*.p12`.

**Folder:** `EliteRestaurant.Api/certs/.gitkeep` — keeps directory in repo without committing secrets.

### Relationship to Finding 4 (CORS)

After enabling HTTPS, each tablet origin must be listed as **`https://<tablet-or-server-LAN-ip>:7194`** (or the port you configure) in **`Cors:AllowedOrigins`**.

---

## Cross-reference: new or notable files

| File | Purpose |
|------|---------|
| `EliteRestaurant.Core/Utils/EmployeePinHasher.cs` | BCrypt hash / verify for employee PINs. |
| `EliteRestaurant.Core/Utils/DatabaseConnectionSecret.cs` | DPAPI protect/unprotect for DB password. |
| `EliteRestaurant.Core/Utils/DatabaseSettingsResolver.cs` | Build Npgsql connection string from settings. |
| `EliteRestaurant.Core/Utils/DatabaseSettingsMigration.cs` | Legacy plaintext → structured + DPAPI. |
| `EliteRestaurant.Core/Utils/PostgresBootstrapPipe.cs` | Parse `host|port|db|user|password` (password may contain `\|`). |
| `EliteRestaurant.Api/docs/HTTPS-LAN.md` | LAN HTTPS export, trust, CORS notes. |
| `EliteRestaurant.Api/certs/.gitkeep` | Placeholder for `elite-lan.pfx` (not committed). |

Notable **updates** (not exhaustive): `AppSettings.cs`, `SettingsManager.cs`, `AppDbContext.cs`, `Program.cs` (API), `ServerPortalController.cs`, `wwwroot/index.html`, `appsettings.json`, `launchSettings.json`, `AppearanceSettingsView*`, `App.xaml.cs`, `.gitignore`.

---

## Environment variables and configuration keys

| Name | Used for |
|------|----------|
| `ELITE_DB_PROVIDER` | Must be `PostgreSql` (with `ELITE_POSTGRES_CONNECTION`) to use env-based DB connection. |
| `ELITE_POSTGRES_CONNECTION` | Full PostgreSQL connection string when env-based config is enabled. |
| `ELITE_LAN_CERTIFICATE_PASSWORD` | Password for the LAN HTTPS PFX file. |
| `Cors:AllowedOrigins` | JSON array of allowed browser origins for the API (scheme + host + port). |
| `LanHttps:CertificatePath` | Path to PFX (default `certs/elite-lan.pfx` under API content root). |
| `LanHttps:HttpPort` / `LanHttps:HttpsPort` | HTTP and HTTPS listen ports (defaults 5223 / 7194). |
| `LanHttps:CertificatePassword` | Optional alternative to env for cert password (prefer env in production). |

---

## Operational checklist after deployment

1. **PINs:** Ensure all employees have **re-saved** PINs after migration if any legacy plaintext remained; verify tablet and WPF login.
2. **CORS:** Add every **real** tablet/browser origin (HTTPS after Finding 7), e.g. `https://192.168.1.40:7194`.
3. **Database:** Prefer **`ELITE_POSTGRES_CONNECTION`** for services; for interactive WPF, after first launch with migration confirm **`app-settings.json`** omits **`PostgreSqlConnectionString`** entirely (legacy is cleared to **`null`**; only structured fields + **`PostgreSqlPasswordProtected`** remain). See [Phase 2 closure verification](#phase-2-closure-verification).
4. **HTTPS:** Export PFX to `EliteRestaurant.Api/certs/elite-lan.pfx`, set **`ELITE_LAN_CERTIFICATE_PASSWORD`**, restart API, **trust** the cert on each tablet, browse via **`https://<host>:7194`**.
5. **Assets:** Confirm server portal loads logo/photo without `?token=` in URLs and that images still load after login.

---

## Phase 2 closure verification

These checks were added before treating Phase 2 as **closed**.

### 1. `app-settings.json` after legacy migration

**Requirement:** After the first load that runs `DatabaseSettingsMigration`, the legacy **`PostgreSqlConnectionString`** key must **disappear** from disk (not remain as an empty string).

**Implementation:** `DatabaseSettings.PostgreSqlConnectionString` is **`string?`** with **`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`**. Clears use **`null`**, not `string.Empty`, because **`""` would still serialize** as a JSON property.

**Files:** `EliteRestaurant.Core/Utils/AppSettings.cs`, `DatabaseSettingsMigration.cs`, `SettingsManager.cs` (save after migrate), `AppearanceSettingsViewModel.cs`, `App.xaml.cs`.

**Manual check:** On a machine that still had a plaintext `PostgreSqlConnectionString` in `%LocalAppData%\EliteRestaurantPro\settings\app-settings.json`, start the app once, then open the JSON file and confirm:

- `PostgreSqlHost`, `PostgreSqlPort`, `PostgreSqlDatabase`, `PostgreSqlUsername`, and `PostgreSqlPasswordProtected` are populated.
- There is **no** `PostgreSqlConnectionString` property (or it is absent after re-save).

### 2. Pipe-separated bootstrap password containing `|`

**Requirement:** First-run / retry dialogs in `App.xaml.cs` accept `host|port|database|username|password` where **password may contain `|`**.

**Implementation:** `PostgresBootstrapPipe.TryParse` in `EliteRestaurant.Core/Utils/PostgresBootstrapPipe.cs` splits on **all** `|`, then rejoins **`parts.Skip(4)`** with `"|"`, so the password is everything after the fourth delimiter.

**Example:**  
Input: `localhost|5432|elite|postgres|pa|ss|word`  
Result: password = `pa|ss|word`.

**Manual check:** Use the bootstrap `InputBox` with a password like `a|b|c` and confirm the app saves and connects (or at least builds a connection string that round-trips through `ApplyBootstrapDatabaseSettings`).

### 3. Employees dialog — PIN field messaging

**Requirement:** When editing an employee who already has a PIN, the UI must not look like a **blank mistake**; it should state that a PIN **is set** and that the user types a new one **only to change** it.

**Implementation:** `EmployeesViewModel` exposes **`PinFieldHelpText`** (bound in `EmployeesView.xaml`) and **`PinStoredOnAccount`**, set when opening the edit dialog if `employee.PinCode` is non-empty. The help text distinguishes **add** vs **edit** and **PIN present** vs **not**.

---

## Document history

- **Phase 2 — Security:** Findings **3, 4, 5, 7** as implemented in the EliteRestaurant repository (WPF admin, ASP.NET Core API, shared Core library).

This file is intended as an **engineering handoff**; pair it with `docs/ARCHITECTURE-REFACTOR-CORE-AND-POSTGRES.md` for broader database and layering context.
