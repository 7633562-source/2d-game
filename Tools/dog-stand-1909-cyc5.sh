#!/usr/bin/env bash
# DOG CYCLE 5 stand, 19.09 20:30. One build (dog-cycle5), then the four-run
# ensemble: the quiet run must stay bit-identical to the accepted 87ac5845 and
# the three walk runs must agree with each other inside the same build.
#   quiet  baseline 87ac58453c43e014ded6032c05187564
#   walk   baseline 34f4df76259d153197d61fdf896fa8bb (accepted, pre-cycle-5)
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
  echo "RUN $LABEL dur=$DUR exit=$? $(grep -a -o 'TRIAL_PROVENANCE_FAILED[^"]*' "$LOGDIR/stand-$LABEL.log" | head -1) $(grep -a -o '"executedSteps":[0-9]*,"fixedDeltaTime":[0-9.]*' "$LOGDIR/stand-$LABEL.log" | head -1)"
}
run "dog_c5_q" 25 -dog 1
for i in 1 2 3; do run "dog_c5_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
echo "=== md5 ==="
md5sum "$OUT"/dog_c5_q.csv "$OUT"/dog_c5_w1.csv "$OUT"/dog_c5_w2.csv "$OUT"/dog_c5_w3.csv
echo "expected quiet 87ac58453c43e014ded6032c05187564 (must match)"
echo DONE
