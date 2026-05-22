# Publishes a small Elite Pro build for first-run / setup testing.
# Settings live next to the .exe (not %LocalAppData%) — see .elite-portable marker.
param(
    [string]$OutputDir = "$PSScriptRoot\..\publish\EliteRestaurantPro-first-run"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$proj = Join-Path $root "EliteRestaurantPro\EliteRestaurantPro.csproj"

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

Write-Host "Publishing framework-dependent build (requires .NET 8 Desktop Runtime)..."
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $OutputDir

$freshSettings = @"
{
  "businessProfile": {
    "restaurantName": "",
    "phone": "",
    "address": "",
    "websiteDomain": "",
    "socialMedia": "",
    "logoPath": "",
    "publicMenuBaseUrl": "https://starfish-app-owtoz.ondigitalocean.app"
  },
  "cloudApi": {
    "baseUrl": "https://starfish-app-owtoz.ondigitalocean.app",
    "accessToken": "",
    "tokenExpiresAtUtc": null
  },
  "database": {
    "provider": "PostgreSql",
    "postgreSqlHost": "",
    "postgreSqlPort": 5432,
    "postgreSqlDatabase": "",
    "postgreSqlUsername": "",
    "postgreSqlPasswordProtected": ""
  },
  "firstSiteSetupCompleted": false
}
"@

Set-Content -Path (Join-Path $OutputDir "app-settings.json") -Value $freshSettings -Encoding UTF8
New-Item -Path (Join-Path $OutputDir ".elite-portable") -ItemType File -Force | Out-Null

@"
Elite Restaurant Pro — first-run test build
==========================================

This folder is intentionally EMPTY of restaurant data (no menu, staff, or orders).

- app-settings.json  = blank profile (cloud URL only)
- .elite-portable    = use settings in THIS folder, not %LocalAppData%

Requires: .NET 8 Desktop Runtime (https://dotnet.microsoft.com/download/dotnet/8.0)

Run: EliteRestaurantPro.exe

First-time setup wizard opens automatically in this portable build until you
complete it once (firstSiteSetupCompleted in app-settings.json).

On production cloud (site already exists), use first-site only against an EMPTY
test database, or use POST /api/setup/new-site for tenant #2.

The large self-contained .exe in EliteRestaurantPro-win-x64 is ~200MB because
it bundles the entire .NET runtime, not your restaurant data.
"@ | Set-Content -Path (Join-Path $OutputDir "README-FIRST-RUN.txt") -Encoding UTF8

$exe = Get-Item (Join-Path $OutputDir "EliteRestaurantPro.exe")
$totalMb = [math]::Round((Get-ChildItem $OutputDir -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Done: $($exe.FullName) ($([math]::Round($exe.Length/1MB,1)) MB exe, $totalMb MB total folder)"
