@echo off
setlocal EnableExtensions

REM --- Edit these for your environment ---
set "PG_BIN=C:\Program Files\PostgreSQL\16\bin"
set "PGHOST=localhost"
set "PGPORT=5432"
set "PGUSER=elite_user"
set "PGDATABASE=elite_restaurant"
set "BACKUP_DIR=C:\EliteRestaurant\backups"
REM Set PGPASSWORD in Task Scheduler action (secure) or uncomment next line for testing only:
REM set "PGPASSWORD=your_password_here"

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%" 2>nul

for /f "tokens=1-3 delims=/ " %%a in ('echo %date%') do set "DS=%%c-%%a-%%b"
for /f "tokens=1-2 delims=:." %%a in ("%time%") do set "TS=%%a-%%b"
set "TS=%TS: =0%"
set "FILENAME=%BACKUP_DIR%\elite-backup-%DS%_%TS%.dump"

"%PG_BIN%\pg_dump.exe" ^
  --host=%PGHOST% ^
  --port=%PGPORT% ^
  --username=%PGUSER% ^
  --dbname=%PGDATABASE% ^
  --format=custom ^
  --file="%FILENAME%"

if errorlevel 1 (
  echo [ERROR] Backup failed — check PostgreSQL connectivity and credentials.
  exit /b 1
)

echo [OK] Backup written to %FILENAME%

REM Keep only last 14 backups (*.dump)
forfiles /P "%BACKUP_DIR%" /M *.dump /D -14 /C "cmd /c del @path" 2>nul

endlocal
exit /b 0
