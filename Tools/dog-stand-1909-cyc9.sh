#!/usr/bin/env bash
# DOG CYCLE 9 stand: the stance force feed-forward (Assets/DogStanceController.cs
# StanceFeedSignals + the DriveWalk call sites + ControlJointStable's feedForward
# argument; Assets/DogTrialRecorder.cs appends the walkStanceFeedDemand column).
# Acceptance of the cycle:
#   quiet bit-identical to 87ac58453c43e014ded6032c05187564 on the 42 SHARED
#   columns (an appended column changes the file md5 by construction),
#   3 walk runs -> ONE md5, and the walk bars measured against dog_c8r_w1 (the
#   same tree without the change: walk md5 bcf1ed0bfddf0f28509b4d408106eb02,
#   walk-window travel 1.82220 m, first fell frame 874).
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
    -fixedDelta 0.005 "$@" > "$LOGDIR/c9-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c9-$LABEL.log" | head -1)"
}
run "dog_c9_q" 25 -dog 1
for i in 1 2 3; do run "dog_c9_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c9_q.csv "$OUT"/dog_c9_w1.csv "$OUT"/dog_c9_w2.csv "$OUT"/dog_c9_w3.csv
echo DONE
