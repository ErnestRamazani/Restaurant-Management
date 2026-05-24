@echo off
REM Double-click this file after extracting a new release ZIP (same folder as EliteRestaurantPro.exe).
setlocal
set "SOURCE=%~dp0EliteRestaurantPro.exe"
set "TARGET=%LOCALAPPDATA%\Programs\Elite Restaurant Pro\EliteRestaurantPro.exe"

if not exist "%SOURCE%" (
    echo EliteRestaurantPro.exe was not found next to this updater.
    echo Extract the full ZIP first, then run this file again.
    pause
    exit /b 1
)

if not exist "%LOCALAPPDATA%\Programs\Elite Restaurant Pro\" (
    echo First-time install: running the full installer...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-EliteRestaurantPro.ps1"
    exit /b %ERRORLEVEL%
)

echo Closing Elite Restaurant Pro if it is open...
taskkill /IM EliteRestaurantPro.exe /F >nul 2>&1
timeout /t 2 /nobreak >nul

echo Updating application...
copy /Y "%SOURCE%" "%TARGET%" >nul
if errorlevel 1 (
    echo Update failed. Close Elite Restaurant Pro and try again.
    pause
    exit /b 1
)

echo.
echo Update complete.
echo Open the app from your desktop shortcut: Elite Restaurant Pro
echo Your restaurant data and sign-in settings were not changed.
echo.
pause
