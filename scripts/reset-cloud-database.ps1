# Wipes ALL tenant data from the configured PostgreSQL database (schema kept).
# Usage:
#   $env:ELITE_POSTGRES_CONNECTION = "Host=...;Port=25060;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
#   .\scripts\reset-cloud-database.ps1

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

Write-Host "WARNING: This deletes every restaurant, employee, order, menu row, and reservation." -ForegroundColor Yellow
$confirm = Read-Host "Type WIPE_ALL_DATA to continue"
if ($confirm -ne "WIPE_ALL_DATA") {
    Write-Host "Cancelled."
    exit 0
}

dotnet run --project (Join-Path $root "Tools\ResetCloudDatabase\ResetCloudDatabase.csproj") -- --confirm WIPE_ALL_DATA
