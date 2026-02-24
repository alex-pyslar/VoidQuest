#!/usr/bin/env bash
# ============================================================
#  VoidQuest – Run Game (Linux / macOS)
#  Launches the pre-built game from the build/ folder.
#
#  Usage:
#    ./run.sh            – launch normally
#    ./run.sh editor     – launch from Godot editor (dev mode)
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "${1:-}" = "editor" ]; then
    GODOT4="${GODOT4:-godot4}"
    echo " [VoidQuest] Launching from Godot editor..."
    "$GODOT4" --path "$SCRIPT_DIR"
    exit 0
fi

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

echo " [VoidQuest] Starting..."
chmod +x "$EXE"
"$EXE"
