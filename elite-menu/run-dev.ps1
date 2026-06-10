# Guest menu on http://localhost:8080 (built into API wwwroot — not Vite :5173).
#
# 1. Start API first (new window):  ..\EliteRestaurant.Api\run-dev.ps1
# 2. Then run this script (builds menu + opens browser on :8080)
#
# Hot-reload UI only (legacy :5173):  npm run dev:hot
# Rebuild on save while API runs:     npm run dev:watch
param(
    [switch]$Foreground
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$apiUp = $false
try {
    $r = Invoke-WebRequest -Uri "http://localhost:8080/api/health" -UseBasicParsing -TimeoutSec 3
    $apiUp = $r.StatusCode -eq 200
} catch { }

if (-not $apiUp) {
    Write-Host "Starting API on http://localhost:8080 in a new window..." -ForegroundColor Yellow
    $apiScript = Join-Path $PSScriptRoot "..\EliteRestaurant.Api\run-dev.ps1"
    & $apiScript
    Start-Sleep -Seconds 4
}

if ($Foreground) {
    npm run dev @args
    exit $LASTEXITCODE
}

Start-Process -FilePath "powershell.exe" -WorkingDirectory $PSScriptRoot -ArgumentList @(
    "-NoLogo",
    "-NoExit",
    "-Command",
    "Write-Host 'elite-menu: build + http://localhost:8080' -ForegroundColor Cyan; npm run dev"
) | Out-Null

Write-Host ""
Write-Host "Guest menu builds into the API and opens at:" -ForegroundColor Green
Write-Host "  http://localhost:8080/" -ForegroundColor Green
Write-Host ""
Write-Host "API must stay running (EliteRestaurant.Api\run-dev.ps1)." -ForegroundColor Cyan
Write-Host "UI hot-reload on :5173 only if you need it:  npm run dev:hot" -ForegroundColor DarkGray
