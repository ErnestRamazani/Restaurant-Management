# Run once after extracting EliteRestaurantPro-Setup.zip (right-click -> Run with PowerShell).
$ErrorActionPreference = "Stop"

$sourceDir = $PSScriptRoot
$exeSource = Join-Path $sourceDir "EliteRestaurantPro.exe"
if (-not (Test-Path $exeSource)) {
    Write-Error "EliteRestaurantPro.exe not found next to this script."
}

$programDir = Join-Path $env:LOCALAPPDATA "Programs\Elite Restaurant Pro"
$settingsDir = Join-Path $env:LOCALAPPDATA "Elite Restaurant Pro\settings"
$settingsPath = Join-Path $settingsDir "app-settings.json"
$freshSettings = Join-Path $sourceDir "app-settings.json"

New-Item -ItemType Directory -Path $programDir -Force | Out-Null
New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null

Copy-Item $exeSource (Join-Path $programDir "EliteRestaurantPro.exe") -Force

# Upgrades: never overwrite an existing release profile (cloud URL, tokens, branding).
$legacySettings = Join-Path $env:LOCALAPPDATA "EliteRestaurantPro\settings\app-settings.json"
if (Test-Path $settingsPath) {
    Write-Host "Keeping existing settings:"
    Write-Host "  $settingsPath"
} elseif (Test-Path $legacySettings) {
    Copy-Item $legacySettings $settingsPath -Force
    Write-Host "Migrated settings from dev profile:"
    Write-Host "  $legacySettings"
} elseif (Test-Path $freshSettings) {
    Copy-Item $freshSettings $settingsPath -Force
    Write-Host "Created fresh settings:"
    Write-Host "  $settingsPath"
} else {
    @'{"firstSiteSetupCompleted":false,"cloudApi":{"baseUrl":"https://starfish-app-owtoz.ondigitalocean.app"}}'@ | Set-Content $settingsPath -Encoding UTF8
    Write-Host "Created default settings:"
    Write-Host "  $settingsPath"
}

$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "Elite Restaurant Pro.lnk"
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $programDir "EliteRestaurantPro.exe"
$shortcut.WorkingDirectory = $programDir
$shortcut.Description = "Elite Restaurant Pro"
$shortcut.Save()

Write-Host ""
Write-Host "Installed to:"
Write-Host "  $programDir"
Write-Host "Fresh settings:"
Write-Host "  $settingsPath"
Write-Host ""
Write-Host "Desktop shortcut created. Launch Elite Restaurant Pro from your desktop."
Write-Host "(This does not use your old EliteRestaurantPro dev folder.)"
