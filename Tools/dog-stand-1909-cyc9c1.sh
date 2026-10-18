#!/usr/bin/env bash
# DOG CYCLE 9 bisect variant C1: the controller change with ControlJointStable's
# BODY restored to the pre-change arithmetic (the extra parameter is accepted and
# unused). The recorder stays at HEAD (42 columns), so the quiet md5 is compared
# with the accepted 87ac58453c43e014ded6032c05187564 directly.
set -u
PROJECT="E:/Games/2D Game/My project"
cd "$PROJECT" || exit 1
bash Tools/impact-build.sh dog-cycle9c1 2>&1 | tail -2
bash "$LOCALAPPDATA/hermes/scripts/dog-write-manifest.sh" 2>&1 | tail -1
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
"$EXE" -batchmode -trial -nographics -duration 25 -label dog_c9c_q \
  -out "$PROJECT/Trials/Runs" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
  -fixedDelta 0.005 -dog 1 > "$PROJECT/Logs/c9c-dog_c9c_q.log" 2>&1
echo "RUN dog_c9c_q exit=$?"
md5sum "$PROJECT/Trials/Runs/dog_c9c_q.csv"
echo "ACCEPTED QUIET = 87ac58453c43e014ded6032c05187564"
echo DONE
