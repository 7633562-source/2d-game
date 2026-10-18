#!/usr/bin/env bash
# ART-BW3R take: the standing art shape (30 s, seed 1, birdCam 0.85, shotRate 25) with optional
# extra player flags. The leg state needs the bird lane's step switches on for the visual arms.
#   _art-bw3r-take.sh <label> visual|plain [extra flags...]
set -uo pipefail
PROJ="E:/Games/2D Game/My project"
RUNS="$PROJ/Trials/Runs"
EXE="$PROJ/Build/Headless/BalanceTrial.exe"
MAN="$PROJ/Build/Headless/build-manifest.json"
TMP="${GROK_BURN:-$LOCALAPPDATA/Temp/grok-burn}"
LABEL="${1:?usage: _art-bw3r-take.sh <label> visual|plain [flags]}"
MODE="${2:?usage: _art-bw3r-take.sh <label> visual|plain [flags]}"
shift 2 || true

if [ -f "$PROJ/Temp/UnityLockfile" ]; then
  echo "REFUSED: the Unity editor has the project open (Temp/UnityLockfile exists)."
  exit 3
fi
i=0
while [ -f "$TMP/trial.lock" ] && [ $i -lt 60 ]; do sleep 10; i=$((i+1)); done
if [ -f "$TMP/trial.lock" ]; then echo "REFUSED: trial.lock has been held for 600 s by another lane."; exit 4; fi
touch "$TMP/trial.lock"
trap 'rm -f "$TMP/trial.lock"' EXIT

FP=$(sed -n 's/.*"fingerprint": *"\([^"]*\)".*/\1/p' "$MAN" | head -1)

if [ "$MODE" = plain ]; then
  VIS=(-nographics)
else
  VIS=(-visual -shotRate 25 -birdCam 0.85 -birdRect 1)
fi

"$EXE" -batchmode -trial -duration 30 -label "$LABEL" -out "$RUNS" \
  -sourceFingerprint "$FP" -buildManifest "$MAN" -fixedDelta 0.005 \
  -bird 1 -birdRig flock -birdDrive wander -birdDriveSeed 1 \
  "$@" "${VIS[@]}" > "$TMP/artbw3r_$LABEL.log" 2>&1
code=$?
echo "player exit=$code label=$LABEL mode=$MODE flags=$* fingerprint=$FP"
ls -1 "$RUNS"/shot_"$LABEL"_*.png 2>/dev/null | wc -l
tail -c 160 "$TMP/artbw3r_$LABEL.log"
exit $code
