#!/usr/bin/env bash
# CLEARING-1 cycle 21.09: build the preview player, then shoot the clearing.
#
#   bash Tools/clearing-cycle.sh [tag]
#
# One change: `Assets/Scenes/Clearing.unity` - the first real location - plus
# the level-layer support it needs (`LevelGenerator.clearingProps`,
# `LevelGrove.PlantClearingProps`, `Assets/ClearingScene.cs`) and the preview
# player's scene list. The player must be rebuilt for the scene to boot, so this
# cycle is a build + a shoot: four stills, a >= 15 s pan (the video this cycle
# owes) and the walk run (the man walks the clearing; its own 10 s clip).
set -uo pipefail
cd "E:/Games/2D Game/My project"
TAG="${1:-clearing1}"
RUNS="Trials/Runs"

bash Tools/gallery-preview-build.sh "$TAG" || { echo "CLEARING ABORT: build failed"; exit 1; }

# Stills + the pan, one boot: the same build makes every frame and the report
# carries its buildGuid.
./Build/Preview/GalleryPreview.exe -batchmode -scene Clearing -capture 1 -label "$TAG" \
  -out "$RUNS" -panSeconds 16.5 -panFps 10 -captureWidth 1920 -captureHeight 1080 \
  -logFile "Logs/$TAG.log" || { echo "CLEARING ABORT: capture failed"; exit 1; }

python Tools/art0-pan-encode.py "$TAG" 10

# The walk run: same build, the yard's own walk drive.
./Build/Preview/GalleryPreview.exe -batchmode -scene Clearing -clearingWalk 1 -label "${TAG}walk" \
  -out "$RUNS" -walkSeconds 10 -captureWidth 1920 -captureHeight 1080 \
  -logFile "Logs/$TAG-walk.log" || { echo "CLEARING ABORT: walk run failed"; exit 1; }

python Tools/art0-pan-encode.py "${TAG}walk" 10
echo "CLEARING CYCLE DONE"
