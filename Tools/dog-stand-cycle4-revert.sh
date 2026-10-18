#!/usr/bin/env bash
# DOG CYCLE 4 revert verification: the file is back at HEAD; do the RUNS come back
# to the accepted traces? Expect quiet 87ac58453c43e014ded6032c05187564 and walk
# 34f4df76259d153197d61fdf896fa8bb.
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[: ]*"[^"]*"' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
run() {
  LABEL="$1"; DUR="$2"; shift 2
  "$EXE" -batchmode -trial -nographics -duration "$DUR" -label "$LABEL" \
    -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
    -fixedDelta 0.005 "$@" > "$LOGDIR/stand-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$?"
}
run "dog_c4r_q" 25 -dog 1
run "dog_c4r_w" 25 -dog 1 -dogWalk 1 -dogWalkTime 1
md5sum "$OUT"/dog_c4r_q.csv "$OUT"/dog_c4r_w.csv
echo "expect quiet 87ac58453c43e014ded6032c05187564"
echo "expect walk  34f4df76259d153197d61fdf896fa8bb"
echo DONE
