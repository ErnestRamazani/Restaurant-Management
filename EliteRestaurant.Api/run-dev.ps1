# Stops a previous API instance (avoids MSB3026 file locks), then starts the API on HTTP :8080.
#
# Default: opens a NEW console window so THIS terminal stays free (e.g. to launch the desktop app).
# Same window (block here, see logs inline):  .\run-dev.ps1 -Foreground
param(
    [switch]$Foreground
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
Get-Process -Name "EliteRestaurant.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

if ($Foreground) {
    dotnet run --launch-profile http @args
    exit $LASTEXITCODE
}

Start-Process -FilePath "powershell.exe" -WorkingDirectory $PSScriptRoot -ArgumentList @(
    "-NoLogo",
    "-NoExit",
    "-Command",
    "dotnet run --launch-profile http"
) | Out-Null

Write-Host "API started in a new PowerShell window (HTTP :8080). This terminal is free."
Write-Host "Close that window to stop the API. File logs: logs\elite-api-*.log"
