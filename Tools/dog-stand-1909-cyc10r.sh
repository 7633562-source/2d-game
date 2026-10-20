#!/usr/bin/env bash
# DOG CYCLE 10 REVERT: the tree back at HEAD for both dog files, rebuilt, and the
# accepted ensemble re-earned on THAT player.
# Acceptance: quiet -> 87ac58453c43e014ded6032c05187564 (42 columns),
# 3/3 walk -> bcf1ed0bfddf0f28509b4d408106eb02 (42 columns), executedSteps 5001.
set -u
PROJECT="E:/Games/2D Game/My project"
cd "$PROJECT" || exit 1
git checkout -- Assets/DogStanceController.cs Assets/DogTrialRecorder.cs
echo "CYCLE 10 markers left: $(grep -c 'CYCLE 10' Assets/DogStanceController.cs Assets/DogTrialRecorder.cs | tr '\n' ' ')"
echo "dog files vs HEAD: $(git status --short Assets/DogStanceController.cs Assets/DogTrialRecorder.cs | wc -l) modified"
echo "HeadlessTrial dogStanceFeed args left: $(grep -c 'dogStanceFeed' Assets/HeadlessTrial.cs)"
bash Tools/impact-build.sh dog-cycle10r 2>&1 | tail -3
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
    -fixedDelta 0.005 "$@" > "$LOGDIR/c10r-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c10r-$LABEL.log" | head -1)"
}
run "dog_c10r_q" 25 -dog 1
for i in 1 2 3; do run "dog_c10r_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
md5sum "$OUT"/dog_c10r_q.csv "$OUT"/dog_c10r_w1.csv "$OUT"/dog_c10r_w2.csv "$OUT"/dog_c10r_w3.csv
echo DONE
