#!/usr/bin/env bash
# ============================================================
#  VoidQuest – Linux / macOS Build Script
#  Exports the project using Godot 4.6 headless CLI.
#
#  Prerequisites:
#    • Godot 4.6 installed (add to PATH or set GODOT4)
#    • Linux/macOS export templates installed inside Godot
#    • .NET 8 SDK installed
#
#  Usage:
#    ./build.sh              – Release build  -> build/VoidQuest.x86_64
#    ./build.sh debug        – Debug  build
#    ./build.sh linux        – Linux release build
# ============================================================

set -euo pipefail

# ── Configure ──────────────────────────────────────────────
GODOT4="${GODOT4:-godot4}"
MODE="${1:-release}"

# Detect platform
OS_NAME="$(uname -s)"
case "$OS_NAME" in
    Linux*)   PRESET="Linux/X11";  OUT="build/VoidQuest.x86_64" ;;
    Darwin*)  PRESET="macOS";      OUT="build/VoidQuest.app"     ;;
    *)        PRESET="Linux/X11";  OUT="build/VoidQuest.x86_64" ;;
esac

mkdir -p build

echo ""
echo " [VoidQuest] Building $MODE > $OUT"
echo " Godot: $GODOT4"
echo ""

if [ "$MODE" = "debug" ]; then
    "$GODOT4" --headless --export-debug "$PRESET" "$OUT"
else
    "$GODOT4" --headless --export-release "$PRESET" "$OUT"
fi

echo ""
echo " [OK] Build complete: $OUT"
