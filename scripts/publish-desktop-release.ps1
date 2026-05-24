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

# Do not ship app-settings.json next to the exe — that enables "portable" mode and a blank profile when
# users run the exe from the ZIP instead of Update.bat + the desktop shortcut.
Copy-Item $installScript (Join-Path $staging "Install-EliteRestaurantPro.ps1") -Force
Copy-Item (Join-Path $PSScriptRoot "installer\Update-EliteRestaurantPro.bat") (Join-Path $staging "Update-EliteRestaurantPro.bat") -Force

@"
Elite Restaurant Pro — Setup
============================

FIRST INSTALL (once per PC):
1. Extract this ZIP.
2. Double-click Install-EliteRestaurantPro.ps1 (Run with PowerShell).
3. Use the desktop shortcut "Elite Restaurant Pro".

UPDATES (when you send a new version):
1. Extract the new ZIP.
2. Double-click Update-EliteRestaurantPro.bat
3. Open the app from the same desktop shortcut.

Do NOT run EliteRestaurantPro.exe directly from the ZIP folder — use the shortcut after install/update.

Settings folder:
  %LocalAppData%\Elite Restaurant Pro\settings\

Upgrades: re-running the installer keeps your existing app-settings.json.
First install on a dev PC may copy from:
  %LocalAppData%\EliteRestaurantPro\settings\

Restaurant data (menu, orders) lives in the cloud — upgrades do not wipe the database.
For a blank cloud site only, complete the first-time setup wizard when prompted.
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
