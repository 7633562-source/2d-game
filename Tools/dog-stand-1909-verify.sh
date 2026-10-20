#!/usr/bin/env bash
# DOG verification stand, 19.09 19:30: the accepted traces on the CURRENT player.
# No rebuild. Other lanes rebuilt at 19:18 (BalanceControllerCaptureWalk.cs 19:10,
# TrialRecorder.cs 19:11), so this re-earns the claim "nothing moved the dog".
#   quiet  87ac58453c43e014ded6032c05187564
#   walk   34f4df76259d153197d61fdf896fa8bb   (3 runs must agree)
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
run "dog_v5_q" 25 -dog 1
for i in 1 2 3; do run "dog_v5_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
echo "=== md5 ==="
md5sum "$OUT"/dog_v5_q.csv "$OUT"/dog_v5_w1.csv "$OUT"/dog_v5_w2.csv "$OUT"/dog_v5_w3.csv
echo "expected: 87ac58453c43e014ded6032c05187564 (quiet)  34f4df76259d153197d61fdf896fa8bb (walk)"
echo DONE
