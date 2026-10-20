#!/usr/bin/env bash
# DOG CYCLE 4b stand: the BAR half alone (geometry stand bar at Bind, self-capture
# deleted, fall test reverted to the accepted thicket). The quiet run is the
# bit-identity witness; the walk run isolates the bar's effect on the get-up.
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
run "dog_c4b_q" 25 -dog 1
run "dog_c4b_1" 25 -dog 1 -dogWalk 1 -dogWalkTime 1
run "dog_c4b_2" 25 -dog 1 -dogWalk 1 -dogWalkTime 1
run "dog_c4b_gu14" 40 -dog 1 -dogWalk 1 -dogWalkTime 1
echo "=== md5 ==="
md5sum "$OUT"/dog_c4b_q.csv "$OUT"/dog_c4b_1.csv "$OUT"/dog_c4b_2.csv "$OUT"/dog_c4b_gu14.csv
echo "=== steps ==="
for L in dog_c4b_q dog_c4b_1 dog_c4b_gu14; do
  echo "$L $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$L.log" | head -1)"
done
echo DONE
