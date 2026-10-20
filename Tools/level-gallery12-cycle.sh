#!/usr/bin/env bash
# ART-1 gallery cycle 20.09: build the art player, then re-shoot the catalogue.
#
#   bash Tools/level-gallery12-cycle.sh [tag]
#
# One change: `Assets/GalleryScene.cs` / `Assets/GalleryCapture.cs` now count the
# MISSING-ART tiles actually DRAWN apart from the inventory rows (they shared one
# counter and the record read 7 where the printed inventory said 6), and the run
# names the drawn tiles. The player must be rebuilt for that to reach the record,
# so this cycle is a build + a re-shoot, unlike the zero-touch refreshes.
set -uo pipefail
cd "E:/Games/2D Game/My project"
TAG="${1:-level-gallery12}"
LABEL="${TAG#level-}"
RUNS="Trials/Runs"

bash Tools/gallery-preview-build.sh "$TAG" || { echo "GALLERY12 ABORT: build failed"; exit 1; }

./Build/Preview/GalleryPreview.exe -batchmode -scene Gallery -capture 1 -label "$LABEL" \
  -out "$RUNS" -panSeconds 16.5 -panFps 10 -captureWidth 1920 -captureHeight 1080 \
  -logFile "Logs/art1-$LABEL.log" || { echo "GALLERY12 ABORT: capture failed"; exit 1; }

python Tools/art0-pan-encode.py "$LABEL" 10
cp -f "$RUNS/$LABEL.mp4" "$RUNS/${LABEL}_pan.mp4"
python Tools/level-gallery-inventory.py "$LABEL"
python Tools/gallery-refresh-check.py gallery11 "$LABEL"
python Tools/capture-still-psnr.py gallery11 "$LABEL"
python Tools/level-gallery-lists.py "$RUNS/$LABEL-gallery.json"
echo "GALLERY12 CYCLE DONE"
