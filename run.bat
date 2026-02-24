@echo off
:: ============================================================
::  VoidQuest – Run Game (Windows)
::  Launches the pre-built game from the build\ folder.
::
::  Usage:
::    run.bat           – launch normally
::    run.bat editor    – launch from Godot editor (dev mode)
:: ============================================================

setlocal

if /i "%~1"=="editor" (
    if not defined GODOT4 set "GODOT4=godot4"
    echo  [VoidQuest] Launching from Godot editor...
    "%GODOT4%" --path "%~dp0"
    goto :end
)

set "EXE=%~dp0build\VoidQuest.exe"

if not exist "%EXE%" (
    echo  [ERROR] build\VoidQuest.exe not found.
    echo  Run build.bat first.
    exit /b 1
)

echo  [VoidQuest] Starting...
start "" "%EXE%"

:end
endlocal
