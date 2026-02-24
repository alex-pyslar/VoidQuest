@echo off
:: ============================================================
::  VoidQuest – Windows Build Script
::  Exports the project using Godot 4.6 headless CLI.
::
::  Prerequisites:
::    • Godot 4.6 installed (or portable .exe placed here)
::    • Windows export templates installed inside Godot
::    • .NET 8 SDK installed
::
::  Usage:
::    build.bat              – Release build  -> build\VoidQuest.exe
::    build.bat debug        – Debug  build   -> build\VoidQuest.exe
:: ============================================================

setlocal

:: ── Configure ──────────────────────────────────────────────
:: Path to the Godot 4 executable. Override with env var GODOT4.
if not defined GODOT4 (
    set "GODOT4=godot4"
)

set "PRESET=Windows Desktop"
set "OUT=build\VoidQuest.exe"
set "MODE=release"

if /i "%~1"=="debug" set "MODE=debug"

:: ── Build ──────────────────────────────────────────────────
echo.
echo  [VoidQuest] Building %MODE% ^> %OUT%
echo  Godot: %GODOT4%
echo.

if "%MODE%"=="debug" (
    "%GODOT4%" --headless --export-debug "%PRESET%" "%OUT%"
) else (
    "%GODOT4%" --headless --export-release "%PRESET%" "%OUT%"
)

if %errorlevel% neq 0 (
    echo.
    echo  [ERROR] Build failed. Make sure:
    echo    1. GODOT4 env var points to a valid Godot 4.6 executable
    echo    2. Windows export templates are installed
    echo    3. .NET 8 SDK is installed
    exit /b %errorlevel%
)

echo.
echo  [OK] Build complete: %OUT%
endlocal
