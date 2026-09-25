#!/usr/bin/env bash
# ART-BW1 take: one wander run on the current build.
#   _art-bw1-take.sh <label> visual  -> the standing film shape (30 s, seed 1, birdCam 0.85, shotRate 25)
#   _art-bw1-take.sh <label> plain   -> -nographics rows only (the fence take)
set -uo pipefail
PROJ="E:/Games/2D Game/My project"
RUNS="$PROJ/Trials/Runs"
EXE="$PROJ/Build/Headless/BalanceTrial.exe"
MAN="$PROJ/Build/Headless/build-manifest.json"
TMP="$LOCALAPPDATA/Temp/grok-burn"
LABEL="${1:?usage: _art-bw1-take.sh <label> visual|plain}"
MODE="${2:?usage: _art-bw1-take.sh <label> visual|plain}"

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
  "${VIS[@]}" > "$TMP/artbw1_$LABEL.log" 2>&1
code=$?
echo "player exit=$code label=$LABEL mode=$MODE fingerprint=$FP"
ls -1 "$RUNS"/shot_"$LABEL"_*.png 2>/dev/null | wc -l
tail -c 220 "$TMP/artbw1_$LABEL.log"
exit $code
