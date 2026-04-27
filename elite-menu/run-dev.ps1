# Starts the Vite dev server (customer menu) on :5173 with proxy to the API on :5223.
#
# Default: opens a NEW console so THIS terminal stays free (e.g. to run the API or the desktop app).
# Same window (block here, see logs inline):  .\run-dev.ps1 -Foreground
param(
    [switch]$Foreground
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Release :5173 if a previous Vite/Node is still listening (avoids "port in use" after a bad exit).
$listeners = @(
    Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
)
if ($listeners) {
    $listeners | ForEach-Object {
        if ($_.OwningProcess) {
            Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Milliseconds 400
}

if ($Foreground) {
    npm run dev @args
    exit $LASTEXITCODE
}

# Keep -Command as plain ASCII. Unicode (e.g. em dashes) in this file can break in Windows
# PowerShell 5.1 if the file is not saved with BOM, and can corrupt nested strings.
Start-Process -FilePath "powershell.exe" -WorkingDirectory $PSScriptRoot -ArgumentList @(
    "-NoLogo",
    "-NoExit",
    "-Command",
    "Write-Host 'elite-menu: Vite (browser opens when server is ready)' -ForegroundColor Cyan; npm run dev"
) | Out-Null

Write-Host ""
Write-Host "A second PowerShell window was started for Vite. If you do not see it, check the taskbar."
Write-Host "This terminal is free. Close that Vite window to stop the dev server."
Write-Host "  Local:  http://localhost:5173/menu/   (API on :5223 for /api)"
Write-Host ""
Write-Host "Run from the elite-menu folder, with leading dot and backslash:  " -NoNewline
Write-Host (".\run-dev.ps1") -ForegroundColor Yellow
Write-Host "  Do not use: npm run dev.ps1. Do not use: run-dev.ps1 without the .\ part."
Write-Host ""
Write-Host "From the repo root (EliteRestaurant) you can run:  " -NoNewline
Write-Host (".\run-elite-menu-dev.ps1") -ForegroundColor Yellow
Write-Host ""
