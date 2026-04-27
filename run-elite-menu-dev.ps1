# One-liner from repo root: starts elite-menu the same as elite-menu\run-dev.ps1
# (new PowerShell window for Vite by default, or  -Foreground  in this window).
param(
    [switch]$Foreground
)

$ErrorActionPreference = "Stop"
$script = Join-Path $PSScriptRoot "elite-menu\run-dev.ps1"
& $script @PSBoundParameters
