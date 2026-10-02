#!/usr/bin/env bash
# DOG CYCLE 9 FINAL state: the tree back at HEAD (both dog files), rebuilt, and
# the accepted ensemble re-earned on THAT player - quiet 25 s + 3 walk runs.
# Acceptance: quiet -> 87ac58453c43e014ded6032c05187564,
# 3/3 walk -> bcf1ed0bfddf0f28509b4d408106eb02, executedSteps 5001.
set -u
PROJECT="E:/Games/2D Game/My project"
cd "$PROJECT" || exit 1
git checkout -- Assets/DogStanceController.cs Assets/DogTrialRecorder.cs
echo "CYCLE 9 markers left in the tree: $(grep -c 'CYCLE 9' Assets/DogStanceController.cs Assets/DogTrialRecorder.cs | tr '\n' ' ')"
echo "dog files vs HEAD: $(git status --short Assets/DogStanceController.cs Assets/DogTrialRecorder.cs | wc -l) modified"
bash Tools/impact-build.sh dog-cycle9final 2>&1 | tail -2
bash "$LOCALAPPDATA/hermes/scripts/dog-write-manifest.sh" 2>&1 | tail -1
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
    -fixedDelta 0.005 "$@" > "$LOGDIR/c9f-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c9f-$LABEL.log" | head -1)"
}
run "dog_c9f_q" 25 -dog 1
for i in 1 2 3; do run "dog_c9f_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c9f_q.csv "$OUT"/dog_c9f_w1.csv "$OUT"/dog_c9f_w2.csv "$OUT"/dog_c9f_w3.csv
echo DONE
