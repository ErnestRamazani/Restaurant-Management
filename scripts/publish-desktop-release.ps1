# Builds the public desktop installer package (fresh settings, not dev %LocalAppData%).
# Output: dist/EliteRestaurantPro-Setup.zip  (one file to upload)
param(
    [string]$OutputDir = "$PSScriptRoot\..\dist",
    [string]$ZipName = "EliteRestaurantPro-Setup.zip"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$proj = Join-Path $root "EliteRestaurantPro\EliteRestaurantPro.csproj"
$staging = Join-Path $OutputDir "setup-staging"
$freshSettings = Join-Path $PSScriptRoot "app-settings.fresh.json"
$installScript = Join-Path $PSScriptRoot "installer\Install-EliteRestaurantPro.ps1"

if (Test-Path $OutputDir) {
    Get-ChildItem $OutputDir -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Write-Host "Publishing release build (isolated settings folder)..."
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:RELEASE_DISTRIBUTION=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $staging

Get-ChildItem $staging -File | Where-Object { $_.Extension -ne ".exe" } | Remove-Item -Force

Copy-Item $freshSettings (Join-Path $staging "app-settings.json") -Force
Copy-Item $installScript (Join-Path $staging "Install-EliteRestaurantPro.ps1") -Force

@"
Elite Restaurant Pro — Setup
============================

1. Extract this ZIP anywhere (Downloads is fine).
2. Right-click Install-EliteRestaurantPro.ps1 -> Run with PowerShell.
   (If blocked: open PowerShell here and run: .\Install-EliteRestaurantPro.ps1)
3. Launch from the new desktop shortcut "Elite Restaurant Pro".

This install uses a NEW settings folder:
  %LocalAppData%\Elite Restaurant Pro\settings\
It does NOT read your dev profile at:
  %LocalAppData%\EliteRestaurantPro\settings\

Restaurant data (menu, orders) still comes from the cloud API after you sign in.
For a blank cloud site, complete the first-time setup wizard when prompted.
"@ | Set-Content (Join-Path $staging "README-INSTALL.txt") -Encoding UTF8

$zipPath = Join-Path $OutputDir $ZipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -Force
Remove-Item $staging -Recurse -Force

$zip = Get-Item $zipPath
$mb = [math]::Round($zip.Length / 1MB, 1)
Write-Host ""
Write-Host "Upload this ONE file:"
Write-Host "  $($zip.FullName)"
Write-Host "  Size: $mb MB"
Write-Host ""
Write-Host "Customers: extract ZIP -> run Install-EliteRestaurantPro.ps1 -> use desktop shortcut."
