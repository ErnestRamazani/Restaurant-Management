# Local stack: guest menu (built into API wwwroot) + API + optional Elite Pro — all on http://localhost:8080
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

function Stop-PortListener([int]$Port) {
    $p = (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1).OwningProcess
    if ($p) {
        Write-Host "Stopping process on port $Port (PID $p)..." -ForegroundColor Yellow
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

Write-Host "Building guest menu into EliteRestaurant.Api/wwwroot..." -ForegroundColor Cyan
Push-Location (Join-Path $root "elite-menu")
npm run build
Pop-Location

Stop-PortListener -Port 8080

Write-Host "Starting API on http://localhost:8080 ..." -ForegroundColor Cyan
Write-Host "  Guest menu:  http://localhost:8080/" -ForegroundColor Green
Write-Host "  Staff portals: http://localhost:8080/server/  (kitchen, cashier, bar, admin, reception)" -ForegroundColor Green
Write-Host "  Swagger:       http://localhost:8080/swagger" -ForegroundColor Green
Write-Host ""
Write-Host "Elite Pro: Cloud API and Public menu URL should be http://localhost:8080 (Appearance settings)." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop the API." -ForegroundColor DarkGray

dotnet run --project (Join-Path $root "EliteRestaurant.Api\EliteRestaurant.Api.csproj") --launch-profile http-lan
