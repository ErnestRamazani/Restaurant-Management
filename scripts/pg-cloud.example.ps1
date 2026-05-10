# Example-only: PostgreSQL dump/restore placeholders.
# Copy this file outside the repo or duplicate it locally; fill in secrets via environment variables or prompts — do not commit passwords.

$ErrorActionPreference = 'Stop'

# --- Source (local) ---
$SourceHost = '<LOCAL_HOST>'
$SourcePort = 5432
$SourceDatabase = 'elite_restaurant'
$SourceUser = '<LOCAL_USER>'
$SourceDumpPath = "$env:TEMP\elite_restaurant.dump"

# --- Target (cloud) ---
$TargetHost = '<CLOUD_HOST>'
$TargetPort = 5432
$TargetDatabase = '<CLOUD_DATABASE>'
$TargetUser = '<CLOUD_USER>'

# Dump (requires pg_dump on PATH)
& pg_dump `
    -h $SourceHost -p $SourcePort -U $SourceUser -d $SourceDatabase `
    -Fc -f $SourceDumpPath

# Restore (requires pg_restore on PATH)
& pg_restore `
    -h $TargetHost -p $TargetPort -U $TargetUser -d $TargetDatabase `
    --no-owner --no-privileges --clean --if-exists `
    $SourceDumpPath

Write-Host "Done. Set DATABASE_URL or ELITE_POSTGRES_CONNECTION for the API to the cloud database."
