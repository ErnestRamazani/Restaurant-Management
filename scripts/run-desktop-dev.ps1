# Run Elite Pro against local API. Stops API file locks by warning if Api process is running.
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$apiProc = Get-Process -Name "EliteRestaurant.Api" -ErrorAction SilentlyContinue
if ($apiProc) {
    Write-Host "Note: EliteRestaurant.Api is running (good for testing). If build fails with file locked, close the API window first." -ForegroundColor Yellow
}

Write-Host "Starting desktop (Debug). Set Cloud API to http://localhost:8080 in Settings." -ForegroundColor Cyan
dotnet run --project (Join-Path $root "EliteRestaurantPro\EliteRestaurantPro.csproj") @args
