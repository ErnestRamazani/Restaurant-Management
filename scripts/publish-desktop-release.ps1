# Builds ONE self-contained .exe for sharing online (no folder of DLLs).
# Output: dist/EliteRestaurantPro.exe  (~80–120 MB, includes .NET 8 runtime)
param(
    [string]$OutputDir = "$PSScriptRoot\..\dist"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$proj = Join-Path $root "EliteRestaurantPro\EliteRestaurantPro.csproj"

if (Test-Path $OutputDir) {
    Get-ChildItem $OutputDir -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Publishing single-file Windows x64 build (self-contained)..."
dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $OutputDir

# Remove stray artifacts; keep only the main executable for upload.
Get-ChildItem $OutputDir -File | Where-Object { $_.Extension -ne ".exe" } | Remove-Item -Force

$exe = Get-Item (Join-Path $OutputDir "EliteRestaurantPro.exe")
$mb = [math]::Round($exe.Length / 1MB, 1)
Write-Host ""
Write-Host "Ready to upload:"
Write-Host "  $($exe.FullName)"
Write-Host "  Size: $mb MB"
Write-Host ""
Write-Host "Users: download EliteRestaurantPro.exe, run it (Windows 10/11 x64). No installer folder."
