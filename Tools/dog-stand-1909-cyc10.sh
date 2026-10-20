#!/usr/bin/env bash
# DOG CYCLE 10: the pose-headroom budget of the stance force feed-forward.
# ONE change (Assets/DogStanceController.cs + DogTrialRecorder.cs + the three
# diagnosis switches in HeadlessTrial.cs), one build, one stand.
#
#   dog_c10_q      25 s, no walk  -> the quiet trace, compared COLUMN-WISE against
#                                   the accepted dog_c8r_q (87ac5845, 42 columns).
#   dog_c10_w1..3  25 s walk      -> the change: one md5 three times over.
#   dog_c10_z      25 s walk, walkStanceFeedShare 0
#                                 -> the diagnosis switch: the force is zero, so
#                                    this run must reproduce the accepted walk
#                                    trace dog_c8r_w1 (bcf1ed0b) column for column.
set -u
PROJECT="E:/Games/2D Game/My project"
cd "$PROJECT" || exit 1
echo "CYCLE 10 markers: $(grep -c 'CYCLE 10' Assets/DogStanceController.cs Assets/DogTrialRecorder.cs | tr '\n' ' ')"
echo "modified dog files: $(git status --short Assets/DogStanceController.cs Assets/DogTrialRecorder.cs Assets/HeadlessTrial.cs | wc -l)"
bash Tools/impact-build.sh dog-cycle10 2>&1 | tail -3
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
    -fixedDelta 0.005 "$@" > "$LOGDIR/c10-$LABEL.log" 2>&1
  echo "RUN $LABEL exit=$? $(grep -a -o '"executedSteps":[0-9]*' "$LOGDIR/c10-$LABEL.log" | head -1)"
}
run "dog_c10_q" 25 -dog 1
for i in 1 2 3; do run "dog_c10_w$i" 25 -dog 1 -dogWalk 1 -dogWalkTime 1; done
run "dog_c10_z" 25 -dog 1 -dogWalk 1 -dogWalkTime 1 -dogStanceFeedShare 0
md5sum "$OUT"/dog_c10_q.csv "$OUT"/dog_c10_w1.csv "$OUT"/dog_c10_w2.csv "$OUT"/dog_c10_w3.csv "$OUT"/dog_c10_z.csv
echo DONE
