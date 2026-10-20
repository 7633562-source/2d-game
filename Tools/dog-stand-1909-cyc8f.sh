#!/usr/bin/env bash
# DOG CYCLE 8 stand: the stance force path (Assets/DogStanceController.cs
# DriveWalkStanceForce + the walk call site; Assets/DogTrialRecorder.cs appends
# the walkStanceForceDemand column). Acceptance of the cycle:
#   quiet bit-identical to 87ac58453c43e014ded6032c05187564 (cycle 7 revert build
#   a7050207 re-earned it, run dog_c8_q),
#   3 walk runs -> ONE md5, and the walk bars measured against dog_c8_w1 (the same
#   tree without the change: walk md5 bcf1ed0bfddf0f28509b4d408106eb02).
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
run() {
  LABEL="$1"; DUR="$2"; shift 2
  "$EXE" -batchmode -trial -nographics -duration "$DUR" -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 "$@" > "$LOGDIR/c8f-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c8f-$LABEL.log" | head -1)"
}
run "dog_c8f_q" 25 -dog 1
for i in 1 2 3; do run "dog_c8f_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c8f_q.csv "$OUT"/dog_c8f_w1.csv "$OUT"/dog_c8f_w2.csv "$OUT"/dog_c8f_w3.csv
echo DONE
