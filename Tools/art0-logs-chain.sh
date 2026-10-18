#!/usr/bin/env bash
# ART-0 nineteenth pass (painted campfire logs): the build + gate chain, bash only.
#
# 1. wait for the trial lane's lock to clear (never fight another lane for the project);
# 2. art0-import-gate.sh   - the twelve shipped pictures through the loaders + the tiling renders;
# 3. art1-gate.sh          - ART-1A-CHECK: every part key, the materials, the level keys, the fire;
# 4. art-gallery-capture.sh - build the preview player from this tree (the compile proof) and
#                             capture the catalogue: 8 stills + the 16.5 s pan.
#
# Usage: bash Tools/art0-logs-chain.sh [label] [lockWaitMinutes]
set -uo pipefail

PROJ="E:/Games/2D Game/My project"
TMP="${GROK_BURN:-$LOCALAPPDATA/Temp/grok-burn}"
LABEL="${1:-art0logs}"
WAIT="${2:-90}"
OUT="$TMP/art0-logs-chain.out"

cd "$PROJ" || exit 1
: > "$OUT"

echo "== waiting for trial.lock (up to ${WAIT} min) $(date +%H:%M:%S)" | tee -a "$OUT"
i=0
while [ -f "$TMP/trial.lock" ]; do
  if [ "$i" -ge $((WAIT * 2)) ]; then
    echo "REFUSED: trial.lock still held after ${WAIT} min - no build run." | tee -a "$OUT"
    exit 4
  fi
  sleep 30
  i=$((i + 1))
done
echo "== lock clear at $(date +%H:%M:%S)" | tee -a "$OUT"

echo "== art0-import-gate.sh $(date +%H:%M:%S)" | tee -a "$OUT"
bash "$LOCALAPPDATA/hermes/scripts/art0-import-gate.sh" 2>&1 | tee -a "$OUT"
echo "art0-gate exit=${PIPESTATUS[0]}" | tee -a "$OUT"

echo "== art1-gate.sh $(date +%H:%M:%S)" | tee -a "$OUT"
bash "$LOCALAPPDATA/hermes/scripts/art1-gate.sh" 2>&1 | tee -a "$OUT"
echo "art1-gate exit=${PIPESTATUS[0]}" | tee -a "$OUT"

echo "== art-gallery-capture.sh $LABEL $(date +%H:%M:%S)" | tee -a "$OUT"
bash "$LOCALAPPDATA/hermes/scripts/art-gallery-capture.sh" "$LABEL" 2>&1 | tee -a "$OUT"
echo "capture exit=${PIPESTATUS[0]}" | tee -a "$OUT"
echo "== done $(date +%H:%M:%S)" | tee -a "$OUT"
