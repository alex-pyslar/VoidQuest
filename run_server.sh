#!/usr/bin/env bash
# ============================================================
#  VoidQuest – Dedicated Server (Linux / macOS)
#  Runs a headless game server that players can join over LAN.
#
#  The server:
#    • Binds ENet on UDP port 7777 (default)
#    • Loads the world scene immediately (no UI)
#    • Accepts up to 8 simultaneous players
#    • Prints all activity to stdout
#
#  Usage:
#    ./run_server.sh          – start server (foreground)
#    ./run_server.sh &        – start in background
#    nohup ./run_server.sh &  – detached (survives SSH logout)
#
#  Players connect from the in-game Lobby → Join Game → enter
#  this machine's LAN IP address.
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

OS_NAME="$(uname -s)"
case "$OS_NAME" in
    Linux*)  EXE="$SCRIPT_DIR/build/VoidQuest.x86_64" ;;
    Darwin*) EXE="$SCRIPT_DIR/build/VoidQuest.app/Contents/MacOS/VoidQuest" ;;
    *)       EXE="$SCRIPT_DIR/build/VoidQuest.x86_64" ;;
esac

if [ ! -f "$EXE" ]; then
    echo " [ERROR] $EXE not found."
    echo " Run ./build.sh first."
    exit 1
fi

echo ""
echo " ╔══════════════════════════════════════════╗"
echo " ║    VOID QUEST  –  Dedicated Server       ║"
echo " ╚══════════════════════════════════════════╝"
echo " Executable : $EXE"
echo " Port       : 7777"
echo " Press Ctrl+C to stop."
echo ""

chmod +x "$EXE"
exec "$EXE" --headless --server
