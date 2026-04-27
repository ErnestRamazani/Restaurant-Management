# Architecture refactor: Core library, operational tooling, PostgreSQL-only

This document describes three coordinated changes to the EliteRestaurant codebase so reviewers can trace **what changed**, **why**, and **where to look in source**. It is intended for code review and onboarding.

---

## Summary table

| Initiative | Goal |
|------------|------|
| **1. `EliteRestaurant.Core`** | Separate shared domain/data from the WPF desktop app so the API and tools do not reference `WinExe` / `UseWPF`. |
| **2. `ClearActiveOrders` + VS Code build** | Make the operational tool honest (PostgreSQL-aware + compiles) and make the default editor build compile **all** projects. |
| **3. Remove SQLite residue** | Single database story: PostgreSQL only; drop dead code paths, `#if false` blocks, and the legacy Npgsql timestamp workaround. |

---

## 1. Extract `EliteRestaurant.Core` and remove API → WPF reference

### Problem

`EliteRestaurant.Api` previously referenced `EliteRestaurantPro.csproj`, a **Windows WPF** executable (`OutputType` `WinExe`, `UseWPF`). That:

- Broke or complicated **API-only** builds (e.g. Linux CI, Docker, headless agents).
- Coupled HTTP endpoints to **WPF/XAML** generation and desktop-only references.

### Solution

Introduced **`EliteRestaurant.Core`** — a **`net8.0`** class library (no WPF) holding:

| Area | Contents |
|------|----------|
| **Models** | `EliteRestaurant.Core/Models/` — EF entity types and DTOs. |
| **Data** | `EliteRestaurant.Core/Data/` — `AppDbContext`, query helpers, financial/payroll services. |
| **Utils** | `EliteRestaurant.Core/Utils/` — business helpers (currency, payroll, order workflow, settings, Excel export, etc.). |

**WPF-only code stayed in the desktop project** — notably `EliteRestaurantPro/Utils/ThemeManager.cs`, which uses `System.Windows` and application resources. It references **`EliteRestaurant.Core.Utils`** for `ThemePalette` (palette JSON / shared DTOs without UI).

### Namespaces

Shared code uses namespaces such as:

- `EliteRestaurant.Core.Models`
- `EliteRestaurant.Core.Data`
- `EliteRestaurant.Core.Utils`

WPF and API code use `using` directives pointing at these namespaces.

### Project references (current)

**API** — only Core, **cross-platform** `net8.0`:

```1:18:EliteRestaurant.Api/EliteRestaurant.Api.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.3" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EliteRestaurant.Core\EliteRestaurant.Core.csproj" />
  </ItemGroup>

</Project>
```

**WPF** — Core + QuestPDF (PDF generation stays in the desktop app):

```1:22:EliteRestaurantPro/EliteRestaurantPro.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationIcon />
    <StartupObject>EliteRestaurantPro.App</StartupObject>
    <Optimize>false</Optimize>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="QuestPDF" Version="2026.2.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EliteRestaurant.Core\EliteRestaurant.Core.csproj" />
  </ItemGroup>

</Project>
```

**SeedRunner** and **Tools/ClearActiveOrders** reference **Core** only (`net8.0` console apps), not the WPF project.

### Model note: `ReservationEntry`

`ReservationEntry` was tied to WPF brushes (`SolidColorBrush`). In Core it is a **plain** model with a **`StatusColor`** string only; UI can bind or convert colors in the WPF layer if needed.

---

## 2. Fix `ClearActiveOrders`, solution-wide build, and VS Code tasks

### Why `ClearActiveOrders` was wrong

Earlier versions printed a **SQLite-era path** (or referenced non-existent members like `DatabasePath`) while **`DeleteAllActiveOrders()`** actually uses the same **`AppDbContext`** as production — **PostgreSQL** when configured. That could mislead operators during a live incident.

### Current behavior

`Tools/ClearActiveOrders/Program.cs`:

1. Calls **`AppDbContext.Initialize()`** so schema matches API/SeedRunner (same startup path as other hosts).
2. Prints **`AppDbContext.GetDatabaseTargetDescription()`** — a **safe** host/port/database label (no password).
3. Calls **`AppDbContext.DeleteAllActiveOrders()`** and reports how many rows were removed.

```1:8:Tools/ClearActiveOrders/Program.cs
using EliteRestaurant.Core.Data;

AppDbContext.Initialize();

Console.WriteLine($"Database: {AppDbContext.GetDatabaseTargetDescription()}");

var removed = AppDbContext.DeleteAllActiveOrders();
Console.WriteLine($"Removed {removed} active order(s) (Waiting / In Kitchen / Ready / Served).");
```

### `GetDatabaseTargetDescription()` (shared helper)

Implemented on **`AppDbContext`** for reuse by any tool:

```448:470:EliteRestaurant.Core/Data/AppDbContext.cs
    /// <summary>
    /// Human-readable database target for operational tools (host/database only; no password).
    /// </summary>
    public static string GetDatabaseTargetDescription()
    {
        if (!TryGetPostgreSqlConnectionString(out var cs))
        {
            return "PostgreSQL (not configured — set ELITE_DB_PROVIDER=PostgreSql and ELITE_POSTGRES_CONNECTION, " +
                   "or Database.PostgreSqlConnectionString in app settings).";
        }

        try
        {
            var b = new NpgsqlConnectionStringBuilder(cs);
            var host = string.IsNullOrWhiteSpace(b.Host) ? "?" : b.Host;
            var db = string.IsNullOrWhiteSpace(b.Database) ? "?" : b.Database;
            return $"PostgreSQL {host}:{b.Port}/{db}";
        }
        catch
        {
            return "PostgreSQL (configured)";
        }
    }
```

Connection resolution follows the same rules as the app: **`ELITE_DB_PROVIDER` + `ELITE_POSTGRES_CONNECTION`**, or **`SettingsManager`** / `app-settings.json` (see `TryGetPostgreSqlConnectionString` in the same file).

### Relationship to `SeedRunner --reduce-active-orders=N`

- **`ClearActiveOrders`**: **deletes** active pipeline orders (per `IsActiveOrderStatus` / `DeleteAllActiveOrders`).
- **`SeedRunner --reduce-active-orders=N`**: **keeps** the *N* newest active orders and **marks the rest as Completed** (different semantics for demos/diagnostics).

They are **not** duplicates; both remain valid for different use cases.

### VS Code build and `EliteRestaurant.sln`

The default **`build`** task in **`.vscode/tasks.json`** runs **`dotnet build`** on **`EliteRestaurant.sln`** so the API, Core, WPF, SeedRunner, and ClearActiveOrders all compile in one shot (API failures are no longer hidden if only the WPF project is built).

```1:15:.vscode/tasks.json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/EliteRestaurant.sln",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary;ForceNoAlign"
            ],
            "problemMatcher": "$msCompile"
        },
```

**`publish`** and **`watch`** still target the WPF project by default (desktop deployment / hot reload).

---

## 3. Remove SQLite NuGet, import flag, `#if false` blocks, and legacy Npgsql timestamps

### Packages

**`Microsoft.EntityFrameworkCore.Sqlite`** was removed from **`EliteRestaurant.Core.csproj`**. Core now carries only what PostgreSQL + Excel need:

```10:15:EliteRestaurant.Core/EliteRestaurant.Core.csproj
  <ItemGroup>
    <PackageReference Include="ClosedXML" Version="0.105.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.*" />
  </ItemGroup>
```

### Startup: SQLite import removed

The **`--import-sqlite-now`** branch and **`AppDbContext.ImportLegacySqliteIntoPostgreSql`** were removed. PostgreSQL is the only runtime store.

### `AppDbContext` configuration

**`OnConfiguring`** only wires **Npgsql** when a connection string is available; otherwise it throws a clear configuration error (no SQLite fallback):

```41:58:EliteRestaurant.Core/Data/AppDbContext.cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        if (TryGetPostgreSqlConnectionString(out var postgresConnectionString))
        {
            optionsBuilder.UseNpgsql(
                postgresConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(5));
            return;
        }

        throw new InvalidOperationException(
            "PostgreSQL is required but no connection string was found. " +
            "Set ELITE_POSTGRES_CONNECTION + ELITE_DB_PROVIDER=PostgreSql " +
            "or configure Database.PostgreSqlConnectionString in app settings.");
    }
```

### Removed `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`

That switch existed to ease migration from ambiguous local `DateTime` behavior. It was removed from:

- `EliteRestaurant.Api/Program.cs`
- `EliteRestaurantPro/App.xaml.cs`
- `SeedRunner/Program.cs`
- `Tools/ClearActiveOrders/Program.cs`

### UTC `DateTime` converters (replacement for legacy timestamp behavior)

Instead of touching every entity property by hand, **`OnModelCreating`** registers **`ValueConverter`** instances for **all** `DateTime` and `DateTime?` properties so values read/written through EF are normalized for **PostgreSQL `timestamptz`** expectations:

```290:318:EliteRestaurant.Core/Data/AppDbContext.cs
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime, DateTime>(
                            v => ToUtcDateTime(v),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? ToUtcDateTime(v.Value) : v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                }
            }
        }
    }

    private static DateTime ToUtcDateTime(DateTime v) =>
        v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };
```

**Logic:** `Utc` passes through; `Local` converts to UTC; `Unspecified` is treated as UTC clock time (appropriate for a pre-production cutover where data is reseeded).

### Dead code: `#if false` in `CreateOrderViewModel`

A large **duplicate** `CreateOrderViewModel` implementation was wrapped in **`#if false`** (~2.9k lines). It was **deleted** entirely. The active implementation is the single class at the top of `EliteRestaurantPro/ViewModels/CreateOrderViewModel.cs`.

### Minor copy / UX

- Dashboard DB status strings now say **PostgreSQL** instead of “Local database” where applicable (`AdminDashboardViewModel`).
- Comments that referenced SQLite in `PayrollSupport` / `StaffLoginViewModel` were updated to describe current behavior.

---

## Verification log (double-check results)

This section records **what was actually run** so reviewers do not have to infer build health from the doc alone.

### `dotnet build EliteRestaurant.sln`

**Last verified:** 2026-04-15 (SDK 8.0.202).

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Projects built in the solution graph: **EliteRestaurant.Core** (`net8.0`), **ClearActiveOrders** (`net8.0`), **SeedRunner** (`net8.0`), **EliteRestaurant.Api** (`net8.0`), **EliteRestaurantPro** (`net8.0-windows`). **WPF is the only `net8.0-windows` project**; it references Core via `ProjectReference` only. There is no requirement for the API or Core to target `net8.0-windows`, and no cross-TFM compile error was observed.

### SQLite references after package removal

A repo-wide search for `Microsoft.EntityFrameworkCore.Sqlite` / `Microsoft.Data.Sqlite` in `*.cs` files returned **no matches** (no orphaned `using` directives).

### `ThemeManager` + `ThemePalette` split

`ThemePalette` lives in **`EliteRestaurant.Core/Utils/ThemePalette.cs`** (`namespace EliteRestaurant.Core.Utils`). **`EliteRestaurantPro/Utils/ThemeManager.cs`** stays in the WPF project and imports **`using EliteRestaurant.Core.Utils`** so it can construct and persist `ThemePalette` while applying brushes via `System.Windows` / `Application.Current.Resources`.

**Build:** `EliteRestaurantPro` is part of the solution build above and compiled successfully.

**Runtime / UI:** Automated UI tests were not run in this verification. The split is structurally sound (WPF-only types only in `ThemeManager`; palette DTO in Core). If you change theme wiring, smoke-test **Appearance** settings and **Apply** / restart.

---

## Npgsql: `EnableRetryOnFailure(5)`

The default `AppDbContext` connection path uses:

```csharp
optionsBuilder.UseNpgsql(
    postgresConnectionString,
    npgsql => npgsql.EnableRetryOnFailure(5));
```

This is in **`EliteRestaurant.Core/Data/AppDbContext.cs`** (`OnConfiguring`). It enables **EF Core’s execution strategy** for transient PostgreSQL failures (e.g. brief LAN hiccups), up to **5** retries. It is **not** SQLite-related; it is part of the PostgreSQL provider configuration. If you need stricter fail-fast behavior (e.g. CI), consider making retry count configurable via environment or settings.

---

## Verification commands

From the repository root:

```bash
dotnet build EliteRestaurant.sln
```

Optional per-project checks:

```bash
dotnet build EliteRestaurant.Core/EliteRestaurant.Core.csproj
dotnet build EliteRestaurant.Api/EliteRestaurant.Api.csproj
dotnet build EliteRestaurantPro/EliteRestaurantPro.csproj
dotnet build SeedRunner/SeedRunner.csproj
dotnet build Tools/ClearActiveOrders/ClearActiveOrders.csproj
```

After schema-only changes, **reseed** when appropriate:

```bash
dotnet run --project SeedRunner
```

(SeedRunner truncates and repopulates; use only when acceptable to wipe data.)

---

## Files reviewers should open first

| File | Why |
|------|-----|
| `EliteRestaurant.Core/EliteRestaurant.Core.csproj` | Shared dependencies, no WPF, no SQLite. |
| `EliteRestaurant.Api/EliteRestaurant.Api.csproj` | API → Core only, `net8.0`. |
| `EliteRestaurantPro/EliteRestaurantPro.csproj` | Desktop → Core + QuestPDF. |
| `EliteRestaurant.Core/Data/AppDbContext.cs` | PostgreSQL-only, UTC converters, `Initialize`, `GetDatabaseTargetDescription`. |
| `Tools/ClearActiveOrders/Program.cs` | Operational tool entry point. |
| `.vscode/tasks.json` | Default `build` → solution. |
| `EliteRestaurant.sln` | All projects in one build graph. |

---

## Employee PINs (BCrypt)

`Employee.PinCode` stores a **BCrypt** hash (`BCrypt.Net-Next`). **`EmployeePinHasher`** in `EliteRestaurant.Core/Utils/EmployeePinHasher.cs` provides `HashForStorage` and `Verify` (legacy plaintext in DB is still accepted until re-saved).

- **API / tablet login:** `TabletAuthService` and `StaffLoginViewModel` match by **ID first**, then **`Verify`** in memory (no SQL PIN comparison).
- **Admin Employees UI:** list shows **PIN ●●●●** only; dialog uses a **PasswordBox** with optional “leave blank to keep” on edit.
- **SeedRunner:** seeds hashed PINs; **`=== SIGN-IN CREDENTIALS ===`** prints plaintext PINs from a **parallel dictionary** keyed by Sign-in ID (for dev reference only).

---

## Code review report: what’s done vs what’s next

The static report **`docs/EliteRestaurant-Code-Review-Report.html`** lists findings by severity. A **status banner** was added at the top of that HTML (2026-04-15) so readers know some items are already fixed. Below is a concise map.

| Report # | Topic | Status (as of refactor) |
|----------|--------|-------------------------|
| **1** | `ClearActiveOrders` / `DatabasePath` | **Done** — tool uses `Initialize`, `GetDatabaseTargetDescription`, `DeleteAllActiveOrders`; builds in solution. |
| **2** | API coupled to WPF `WinExe` | **Done** — API references **`EliteRestaurant.Core`** only; `net8.0`. |
| **3** | Employee PINs plaintext | **Done** — BCrypt storage, `Verify` on login, masked list + PasswordBox in admin UI. |
| **4** | API auth, CORS, tokens in URLs | **Open** — design change. |
| **5** | DB connection string in plaintext settings | **Open** — secret handling. |
| **6** | `AppDbContext` responsibilities / migrations | **Partially addressed** — SQLite import engine removed; still uses `EnsureCreated` + raw SQL patches (EF migrations remain a future step). |
| **7** | `CreateOrderViewModel` god object + `#if false` | **Partial** — **dead `#if false` block removed**; class size / extraction still valid follow-ups. |
| **8** | Shared draft retention | **Open** — persistence hygiene. |
| **9** | Duplicated login logic API vs desktop | **Open** — consolidation. |

**Suggested order for “continue” work** (balance impact vs effort):

1. **Tighten API surface** (finding 4) if the API is exposed beyond a trusted LAN — CORS, token transport, standard auth middleware.
2. **Secrets** (finding 5) — environment-first connection strings for server deployments; redacted UI.
3. **PIN handling** (finding 3) — hash-at-rest + never display raw PIN in grids.
4. **EF migrations** (finding 6) — replace ad hoc `EnsureCreated` + raw patches when you’re ready to version schema formally.
5. **Shared drafts** (finding 8) — update-in-place or retention job.
6. **Split `CreateOrderViewModel`** (finding 7) — extract services when touching that area anyway.

---

## Document history

- **2026-04-15** — Written to capture the Core extraction, ClearActiveOrders + solution build, and PostgreSQL-only / SQLite removal work.
- **2026-04-15** — Added verification log (clean `dotnet build` output, TFM note, SQLite `using` scan), ThemeManager split notes, and `EnableRetryOnFailure(5)` explanation.
- **2026-04-15** — Added code review mapping table and “what’s next” priorities; updated `EliteRestaurant-Code-Review-Report.html` with a green status banner.
- **2026-04-15** — Employee PINs: BCrypt (`EmployeePinHasher`), login verification, admin UI masking / PasswordBox, SeedRunner + bootstrap hashing; finding 3 addressed.
