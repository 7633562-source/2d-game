#!/usr/bin/env bash
# DOG CYCLE 4 stand (19.09): the accepted runs on the cycle-4 build.
# Build is NOT rebuilt here (Tools/impact-build.sh already ran).
#   dog_c4_1..5   25 s walk  - the acceptance + the fall rate
#   dog_q4_q      25 s quiet - A-quiet: must be bit-identical to dog_q4_base
#   dog_c4_gu14   40 s walk  - the long run, the fall rate over 40 s
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
GUID=$(grep -o '"buildGuid"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP GUID=$GUID"
run() {
  LABEL="$1"; DUR="$2"; shift 2
  "$EXE" -batchmode -trial -nographics -duration "$DUR" -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 "$@" > "$LOGDIR/stand-$LABEL.log" 2>&1
  echo "RUN $LABEL dur=$DUR exit=$? $(grep -a -o 'TRIAL_PROVENANCE_FAILED[^"]*' "$LOGDIR/stand-$LABEL.log" | head -1)"
}
for i in 1 2 3 4 5; do run "dog_c4_$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
run "dog_q4_q" 25 -dog 1
run "dog_c4_gu14" 40 -dog 1 -dogWalk 1 -dogWalkTime 1
echo "=== md5 (cycle 4 batch) ==="
md5sum "$OUT"/dog_c4_*.csv "$OUT"/dog_q4_q.csv
echo "=== steps ==="
for L in dog_c4_1 dog_q4_q dog_c4_gu14; do
  echo "$L $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$L.log" | head -1)"
done
echo DONE
