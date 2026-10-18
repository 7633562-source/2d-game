#!/usr/bin/env bash
# DOG CYCLE 8 baseline stand: re-earn the accepted state on the CURRENT player
# (the tree took other-lane writes at 23:18/23:22: Assets/TrialRecorder.cs,
# Assets/BalanceControllerCaptureWalk.cs; the player on disk is 23:24, guid
# a705020725b54fddb6acdb0e7dce1c3d). No source change: this is the "does the
# green state still hold" stand, the same shape as Tools/dog-stand-1909-verify.sh.
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
    -fixedDelta 0.005 "$@" > "$LOGDIR/c8-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c8-$LABEL.log" | head -1)"
}
run "dog_c8_q" 25 -dog 1
for i in 1 2 3; do run "dog_c8_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c8_q.csv "$OUT"/dog_c8_w1.csv "$OUT"/dog_c8_w2.csv "$OUT"/dog_c8_w3.csv
echo DONE
