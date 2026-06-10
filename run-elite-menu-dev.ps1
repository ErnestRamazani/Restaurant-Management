# From repo root: build guest menu and open http://localhost:8080 (starts API if needed).
param(
    [switch]$Foreground
)

$ErrorActionPreference = "Stop"
$script = Join-Path $PSScriptRoot "elite-menu\run-dev.ps1"
& $script @PSBoundParameters
