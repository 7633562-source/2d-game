#!/usr/bin/env bash
# DOG CYCLE 10 - dose-response on the SAME player (no rebuild): the same walk with
# the stance force feed-forward set to 0.10 / 0.25 of the share, so the report can
# say whether the cost is proportional to the force or a cliff. Diagnosis only.
set -u
PROJECT="E:/Games/2D Game/My project"
cd "$PROJECT" || exit 1
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
run() {
  LABEL="$1"; shift
  "$EXE" -batchmode -trial -nographics -duration 25 -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 -dog 1 -dogWalk 1 -dogWalkTime 1 "$@" > "$LOGDIR/c10-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c10-$LABEL.log" | head -1)"
}
run dog_c10_h10 -dogStanceFeedShare 0.1
run dog_c10_h25 -dogStanceFeedShare 0.25
md5sum "$OUT"/dog_c10_h10.csv "$OUT"/dog_c10_h25.csv
echo DONE
