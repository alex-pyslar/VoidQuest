@echo off
:: ============================================================
::  VoidQuest – Dedicated Server (Windows)
::  Runs a headless game server that players can join over LAN.
::
::  The server:
::    • Binds ENet on UDP port 7777 (default)
::    • Loads the world scene immediately (no UI)
::    • Accepts up to 8 simultaneous players
::    • Prints all activity to the console
::
::  Usage:
::    run_server.bat           – start server on port 7777
::    run_server.bat 9000      – start server on custom port (WIP)
::
::  Players connect from the in-game Lobby → Join Game → enter
::  this machine's LAN IP address.
:: ============================================================

setlocal

set "EXE=%~dp0build\VoidQuest.console.exe"

if not exist "%EXE%" (
    echo  [ERROR] build\VoidQuest.console.exe not found.
    echo  Run build.bat first.
    exit /b 1
)

echo.
echo  ╔══════════════════════════════════════════╗
echo  ║    VOID QUEST  –  Dedicated Server       ║
echo  ╚══════════════════════════════════════════╝
echo  Executable : %EXE%
echo  Port       : 7777
echo  Press Ctrl+C to stop.
echo.

"%EXE%" --headless --server

endlocal
