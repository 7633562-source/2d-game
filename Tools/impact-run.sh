#!/usr/bin/env bash
# BALANCE-ARENA-1 runner. Mirrors Tools/run-trial.ps1's invocation (the player needs
# -trial, the fingerprint and the manifest path), from bash so the exit code is real.
#
#   Tools/impact-run.sh <label> [duration] [--visual] [extra switches...]
#
# The build is NOT rebuilt: run the Unity line from the card first. Every run prints
# the fingerprint the player was given, so a run can never be quoted against a build
# it did not measure.
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"

LABEL="${1:?usage: impact-run.sh <label> [duration] [--visual] [extra...]}"
shift
DURATION="20"
if [ $# -gt 0 ] && [[ "$1" =~ ^[0-9.]+$ ]]; then DURATION="$1"; shift; fi
VISUAL=0
if [ $# -gt 0 ] && [ "$1" = "--visual" ]; then VISUAL=1; shift; fi

FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
if [ -z "$FP" ]; then echo "no fingerprint in $MANIFEST"; exit 2; fi

ARGS=(-batchmode -trial)
if [ "$VISUAL" = "0" ]; then ARGS+=(-nographics); else ARGS+=(-visual); fi
ARGS+=(-duration "$DURATION" -label "$LABEL" -out "$OUT"
       -sourceFingerprint "$FP" -buildManifest "$MANIFEST" -fixedDelta 0.005)
ARGS+=("$@")

LOG="$PROJECT/Logs/impact-run-$LABEL.log"
echo "RUN $LABEL duration=$DURATION visual=$VISUAL fingerprint=$FP"
"$EXE" "${ARGS[@]}" > "$LOG" 2>&1
CODE=$?
echo "EXIT=$CODE log=$LOG"
grep -a "TRIAL_PROVENANCE_FAILED" "$LOG" | head -3
grep -a "IMPACT_SCENE" "$LOG" | head -1
grep -a "IMPACT_SUMMARY" "$LOG" | head -1 | cut -c1-320
if [ "$VISUAL" = "1" ]; then
  echo "FRAMES=$(ls "$OUT"/shot_"$LABEL"_*.png 2>/dev/null | wc -l)"
fi
exit $CODE
