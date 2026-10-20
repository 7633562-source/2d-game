#!/usr/bin/env bash
# DOG CYCLE 3 stand (19.09 17:20): the ensemble on the new build + the two
# acceptance scenarios of the capture-point sensor. Build is NOT rebuilt here.
#   dog_c3_1..5   25 s walk  - the ensemble / physics-identity stand
#   dog_gu15      25 s quiet - acceptance A3: margin > 0 for >= 99 % of frames
#   dog_gu14c3    40 s walk  - the sustained negative excursion at the fall
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
for i in 1 2 3 4 5; do run "dog_c3_$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
run "dog_gu15" 25 -dog 1
run "dog_gu14c3" 40 -dog 1 -dogWalk 1 -dogWalkTime 1
echo "=== md5 (new batch) ==="
md5sum "$OUT"/dog_c3_*.csv
echo "=== steps ==="
for L in dog_c3_1 dog_gu15 dog_gu14c3; do
  echo "$L $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$L.log" | head -1)"
done
echo DONE
