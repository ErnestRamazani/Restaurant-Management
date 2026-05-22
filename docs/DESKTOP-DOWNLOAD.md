# Elite Restaurant Pro — desktop download

## Why the old `.exe` showed logo, background, and old data

Double-clicking only `EliteRestaurantPro.exe` on **your dev PC** loaded settings from:

```text
%LocalAppData%\EliteRestaurantPro\settings\app-settings.json
```

That folder is from earlier development runs (logo paths, background image, tokens). The `.exe` did not bundle that data — Windows reused it.

Published **release** builds now use a **separate** folder:

```text
%LocalAppData%\Elite Restaurant Pro\settings\
```

so installs stay empty on your machine too.

## Build the file to put online (one ZIP)

From the repo root:

```powershell
.\scripts\publish-desktop-release.ps1
```

Output:

```text
dist/EliteRestaurantPro-Setup.zip
```

Upload **that ZIP** (one download). Do not upload a lone `.exe` unless you tell users to run the installer script inside the ZIP.

## What customers do (install steps)

1. Download `EliteRestaurantPro-Setup.zip`.
2. Extract the ZIP.
3. Right-click **`Install-EliteRestaurantPro.ps1`** → **Run with PowerShell**.
4. Open the desktop shortcut **Elite Restaurant Pro**.

The installer copies the app to `%LocalAppData%\Programs\Elite Restaurant Pro\` and writes a **blank** `app-settings.json`.

## First launch

- Local profile: empty (no logo/background until they configure Appearance).
- Cloud: if the database is empty, the **first-time setup** wizard runs.
- If they sign in to an existing cloud site, menu/staff data loads from the API (expected).

## Rebuild after code changes

Run the script again and replace the ZIP on your download host.

## Developers

| Profile | Settings path |
|--------|----------------|
| Debug / dev runs | `%LocalAppData%\EliteRestaurantPro\settings\` |
| Installed release build | `%LocalAppData%\Elite Restaurant Pro\settings\` |

To test a release build without installing, extract the ZIP and run `Install-EliteRestaurantPro.ps1` once.
