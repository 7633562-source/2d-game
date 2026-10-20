#!/usr/bin/env bash
# DOG CYCLE 6 stand, 19.09. The change: the stance sweep is spent per limb in
# proportion to the load that limb measurably carries (pawLoadFraction), so the
# push follows the contact instead of being split over a diagonal that is half
# in the air. Acceptance: quiet must stay 87ac58453c43e014ded6032c05187564, the
# walk window travel must not fall below 1.65401028 m, and the >= 2-paw /
# diagonal / contact-run bars must move.
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
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$LABEL.log" | head -1)"
}
run "dog_c6_q" 25 -dog 1
for i in 1 2 3; do run "dog_c6_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c6_q.csv "$OUT"/dog_c6_w1.csv "$OUT"/dog_c6_w2.csv "$OUT"/dog_c6_w3.csv
echo DONE
