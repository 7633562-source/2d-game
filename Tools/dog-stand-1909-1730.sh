#!/usr/bin/env bash
# DOG stand 19.09 (orchestrator): re-verify the reference ensemble on the current
# player (fp from the live manifest) and measure the QUIET run baseline that the
# cycle-3 capture-point acceptance is written against. No source edit, no rebuild.
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
  LABEL="$1"; shift
  "$EXE" -batchmode -trial -nographics -duration 25 -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 "$@" > "$LOGDIR/stand-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o 'TRIAL_PROVENANCE_FAILED[^"]*' "$LOGDIR/stand-$LABEL.log" | head -1)"
}
for i in 1 2 3 4 5; do run "dog_art1_r6$i" -dog 1 -dogWalk 1 -dogWalkTime 1; done
run "dog_gu15_base1" -dog 1
echo "=== md5 walk ensemble ==="
md5sum "$OUT"/dog_art1_r6*.csv
echo "=== md5 quiet ==="
md5sum "$OUT"/dog_gu15_base1.csv
echo "=== steps ==="
for L in dog_art1_r61 dog_gu15_base1; do
  echo "$L $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$L.log" | head -1)"
done
echo DONE
