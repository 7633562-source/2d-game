#!/usr/bin/env bash
# ART-1 gallery preview build, from bash.
#
#   bash Tools/gallery-preview-build.sh [tag]
#
# The same shape as Tools/impact-build.sh, and for the same reason (the card's
# rule: builds from bash only - the Unity editor detaches from a PowerShell
# parent and the exit code is lost). It builds Build/Preview/GalleryPreview.exe
# through GalleryPreviewBuild.Build instead of the trial player, so no trial
# number can move: SampleScene is not in this player at all.
#
# It takes the same build lock the compile gate uses and refuses to start while
# a Unity EDITOR holds the project (the Hub does not count). It deliberately does
# NOT write build-status.txt: that file is the TRIAL player's freshness verdict,
# and this build does not produce the trial player.
set -uo pipefail

PROJ="E:/Games/2D Game/My project"
UNITY="F:/6000.6.4f1/Editor/Unity.exe"
TMP="${GROK_BURN:-$LOCALAPPDATA/Temp/grok-burn}"
TAG="${1:-gallery-preview}"
LOG="$TMP/$TAG.log"
LOCKD="$TMP/build.lock"

mkdir -p "$TMP"

editors=$(tasklist /FO CSV /NH 2>/dev/null | tr -d '\000' | grep -ac "Unity.exe" || true)
if [ "${editors:-0}" != "0" ]; then
  echo "PREVIEW_BUILD_SKIPPED: a Unity editor is running"
  exit 3
fi

GOT=0
for _ in $(seq 1 40); do
  if mkdir "$LOCKD" 2>/dev/null; then GOT=1; break; fi
  echo "waiting for the build lock"
  sleep 15
done
if [ "$GOT" != "1" ]; then
  echo "PREVIEW_BUILD_SKIPPED: the build lock is held"
  exit 3
fi
trap 'rmdir "$LOCKD" 2>/dev/null' EXIT

rm -f "$LOG"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" \
  -executeMethod GalleryPreviewBuild.Build -buildOut "Build/Preview" \
  -logFile "$LOG" -quit >> "$TMP/$TAG-console.log" 2>&1
code=$?

if grep -q "PREVIEW_BUILD_OK" "$LOG" 2>/dev/null; then
  echo "PREVIEW_BUILD_OK"
  grep -m1 "PREVIEW_BUILD_OK" "$LOG"
  # the newest source the player was built from, for the report's identity line
  find "$PROJ/Assets" -type f -name '*.cs' -printf '%T@ %TY-%Tm-%Td %TH:%TM\n' 2>/dev/null \
    | sort -rn | head -1 | cut -d' ' -f2-
  exit 0
else
  echo "PREVIEW_BUILD_FAILED exit=$code"
  grep -m5 "error CS\|PREVIEW_BUILD_FAILED\|Exception" "$LOG" 2>/dev/null
  exit 1
fi
