#!/usr/bin/env bash
# Dog determinism runner (harness cycle, 19.09). The step clock must make N runs
# of the same label on ONE build byte-identical, so this runs the same command N
# times and prints the exit code and the fingerprint the player was given for
# each. Mirrors Tools/impact-run.sh's invocation - run-trial.ps1 -Rebuild is
# broken from PowerShell (Unity detaches, empty exit code), so the player is
# invoked directly from bash.
#
#   Tools/dog-det-run.sh [labelPrefix] [count] [duration] [extra switches...]
#   Tools/dog-det-run.sh dog_det 5 25                     # 5 identical runs
#   Tools/dog-det-run.sh dog_det_vis 1 25 -visual         # one rendered run
#
# The build is NOT rebuilt. Check the fingerprint the manifest carries first.
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"

PREFIX="${1:-dog_det}"
COUNT="${2:-5}"
DURATION="${3:-25}"
shift 3 2>/dev/null || true
EXTRA=("$@")

FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
if [ -z "$FP" ]; then echo "no fingerprint in $MANIFEST"; exit 2; fi
GUID=$(grep -o '"buildGuid"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "build fingerprint=$FP guid=$GUID"

for i in $(seq 1 "$COUNT"); do
  LABEL="${PREFIX}${i}"
  LOG="$LOGDIR/dog-det-$LABEL.log"
  "$EXE" -batchmode -trial -nographics -duration "$DURATION" -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 -dog 1 -dogWalk 1 -dogWalkTime 1 "${EXTRA[@]}" \
    > "$LOG" 2>&1
  CODE=$?
  BAD=$(grep -a -o "TRIAL_PROVENANCE_FAILED[^\"]*" "$LOG" | head -1)
  SUM=$(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOG" | head -1)
  echo "RUN $LABEL exit=$CODE ${BAD} ${SUM}"
done
