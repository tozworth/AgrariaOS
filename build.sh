#!/usr/bin/env bash
# Iterative debug build: builds Release and deploys straight into the game's Mods folder.
# Usage:
#   ./build.sh                 build + deploy (default game path below)
#   ./build.sh --no-deploy     build + zip only (dist/)
#   GAME_PATH='/path/to/Stardew Valley' ./build.sh
set -euo pipefail
cd "$(dirname "$0")"

GAME_PATH="${GAME_PATH:-/mnt/sd/kiolin/SteamLibrary/steamapps/common/Stardew Valley}"
DEPLOY=true
[[ "${1:-}" == "--no-deploy" ]] && DEPLOY=false

if [[ ! -f "$GAME_PATH/Stardew Valley.dll" ]]; then
  echo "Game not found at: $GAME_PATH" >&2
  echo "Set GAME_PATH to your Stardew folder and retry." >&2
  exit 1
fi

if pgrep -x "Stardew Valley" >/dev/null || pgrep -x "StardewModdingAPI" >/dev/null || pgrep -x "StardewValley" >/dev/null; then
  echo "Stardew looks running - exit the game first (locked DLLs won't update)." >&2
  exit 1
fi

# Preserve existing config across deploys.
MOD_DIR="$GAME_PATH/Mods/AgrariaOS"
CONFIG_BAK=""
if [[ -f "$MOD_DIR/config.json" ]]; then
  CONFIG_BAK="$(mktemp)"
  cp "$MOD_DIR/config.json" "$CONFIG_BAK"
fi

ARGS=(build AgrariaOS.csproj -c Release "-p:GamePath=$GAME_PATH")
[[ "$DEPLOY" == true ]] && ARGS+=("-p:EnableModDeploy=true")
dotnet "${ARGS[@]}"

if [[ -n "$CONFIG_BAK" && -f "$MOD_DIR/config.json" ]]; then
  cp "$CONFIG_BAK" "$MOD_DIR/config.json"
  rm -f "$CONFIG_BAK"
fi

echo "---"
if [[ "$DEPLOY" == true ]]; then
  echo "Deployed to $MOD_DIR"
  ls "$MOD_DIR"
  echo "Launch the game; on trouble: tail ~/.config/StardewValley/ErrorLogs/SMAPI-latest.txt"
else
  echo "Packaged in dist/ (not deployed)."
fi
