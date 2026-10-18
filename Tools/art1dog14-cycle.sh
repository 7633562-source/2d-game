#!/usr/bin/env bash
# ART-1 dog row cycle 21.09: build the art player, then re-shoot the catalogue.
#
#   bash Tools/art1dog14-cycle.sh [tag]
#
# One change: `Assets/GalleryScene.cs` BuildCreatureRow now draws the dog's ten part
# sprites (`Art/Dog/<part>.png` via ArtLibrary.Tiled + SpriteDrawMode.Sliced, fitted to
# the shipping collider box each `Dog.cs <field>Size` hands to HumanSegment) the way
# BuildHumanRow draws the human's, and the dog's PendingTile and its one missing row are
# gone; `Assets/GalleryCapture.cs` gains the row's two lenses (`_creatures.png`, `_dog.png`).
# The record only moves when the player is rebuilt, so this cycle is a build + a re-shoot,
# like level-gallery12 was.
#
# The label is `art1dog14` (the artist's own series: 13 was the contact-sheet label) so no
# level-lane label is consumed. Every artefact lands in Trials/Runs/ next to the others.
set -uo pipefail
cd "E:/Games/2D Game/My project"
TAG="${1:-art1dog14}"
LABEL="${TAG#level-}"
RUNS="Trials/Runs"

bash Tools/gallery-preview-build.sh "$TAG" || { echo "ART1DOG ABORT: build failed"; exit 1; }

./Build/Preview/GalleryPreview.exe -batchmode -scene Gallery -capture 1 -label "$LABEL" \
  -out "$RUNS" -panSeconds 16.5 -panFps 10 -captureWidth 1920 -captureHeight 1080 \
  -logFile "Logs/art1-$LABEL.log" || { echo "ART1DOG ABORT: capture failed"; exit 1; }

python Tools/art0-pan-encode.py "$LABEL" 10
cp -f "$RUNS/$LABEL.mp4" "$RUNS/${LABEL}_pan.mp4"
python Tools/level-gallery-inventory.py "$LABEL"
python Tools/gallery-refresh-check.py gallery12 "$LABEL"
python Tools/capture-still-psnr.py gallery12 "$LABEL"
python Tools/art1-fit-audit.py --markdown > "$RUNS/$LABEL-fit-audit.md"
echo "ART1DOG CYCLE DONE tag=$TAG"
