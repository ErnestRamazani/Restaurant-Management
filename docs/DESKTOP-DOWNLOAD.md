# Elite Restaurant Pro — desktop download

## Build the shareable file (one `.exe`)

From the repo root in PowerShell:

```powershell
.\scripts\publish-desktop-release.ps1
```

Output:

```text
dist/EliteRestaurantPro.exe
```

Upload **only that file** to your website, Google Drive, Dropbox, or DigitalOcean Spaces. Customers download and double-click — no zip of DLLs, no installer wizard.

| Item | Detail |
|------|--------|
| OS | Windows 10/11, 64-bit |
| Size | ~80–120 MB (includes .NET 8 runtime) |
| Install | None — run the `.exe` directly |
| Settings | Stored under `%LocalAppData%\EliteRestaurantPro\settings\` after first run |

## First launch

1. Run `EliteRestaurantPro.exe`.
2. If the cloud database is empty, the **first-time setup** wizard runs (restaurant name, domain, admin PIN).
3. Otherwise sign in with your admin credentials.

## Rebuild after code changes

Run the script again, then replace the file on your download host.

## Old publish folders (removed)

Do not use `publish/EliteRestaurantPro-first-run` or `publish/EliteRestaurantPro-win-x64` — those were dev/test folder publishes. Use `dist/EliteRestaurantPro.exe` instead.
