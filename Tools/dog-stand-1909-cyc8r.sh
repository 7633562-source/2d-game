#!/usr/bin/env bash
# DOG CYCLE 8 revert stand: the force path is out of the tree (Assets/DogStanceController.cs
# and Assets/DogTrialRecorder.cs == HEAD, the cycle-6 commit). The accepted state must come
# back on THIS build: quiet 87ac58453c43e014ded6032c05187564 and 3/3
# bcf1ed0bfddf0f28509b4d408106eb02 (the cycle-6 ensemble).
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
GUID=$(grep -o '"buildGuid"[^,]*' "$PROJECT/Build/Headless/build-info.json" | cut -d'"' -f4)
echo "FP=$FP GUID=$GUID"
run() {
  LABEL="$1"; DUR="$2"; shift 2
  "$EXE" -batchmode -trial -nographics -duration "$DUR" -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 "$@" > "$LOGDIR/c8r-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c8r-$LABEL.log" | head -1)"
}
run "dog_c8r_q" 25 -dog 1
for i in 1 2 3; do run "dog_c8r_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c8r_q.csv "$OUT"/dog_c8r_w1.csv "$OUT"/dog_c8r_w2.csv "$OUT"/dog_c8r_w3.csv
echo DONE
