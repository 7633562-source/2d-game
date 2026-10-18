#!/usr/bin/env bash
# DOG CYCLE 9 bisect: the CONTROLLER half of the change only (DogTrialRecorder.cs
# stays at HEAD, so the CSV keeps 42 columns and the quiet md5 is compared
# directly with the accepted 87ac58453c43e014ded6032c05187564).
set -u
PROJECT="E:/Games/2D Game/My project"
L="${GROK_BURN:-$LOCALAPPDATA/Temp/grok-burn}"
cp "$L/d9_saved/DogStanceController.cs" "$PROJECT/Assets/DogStanceController.cs"
git -C "$PROJECT" checkout -- Assets/DogTrialRecorder.cs
echo "controller CYCLE 9 markers: $(grep -c 'CYCLE 9' "$PROJECT/Assets/DogStanceController.cs")"
echo "recorder  CYCLE 9 markers: $(grep -c 'CYCLE 9' "$PROJECT/Assets/DogTrialRecorder.cs")"
cd "$PROJECT" || exit 1
bash Tools/impact-build.sh dog-cycle9a 2>&1 | tail -2
bash "$LOCALAPPDATA/hermes/scripts/dog-write-manifest.sh" 2>&1 | tail -1
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
"$EXE" -batchmode -trial -nographics -duration 25 -label dog_c9a_q \
  -out "$PROJECT/Trials/Runs" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
  -fixedDelta 0.005 -dog 1 > "$PROJECT/Logs/c9a-dog_c9a_q.log" 2>&1
echo "RUN dog_c9a_q exit=$?"
md5sum "$PROJECT/Trials/Runs/dog_c9a_q.csv"
echo "ACCEPTED QUIET = 87ac58453c43e014ded6032c05187564"
echo DONE
