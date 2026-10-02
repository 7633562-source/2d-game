#!/usr/bin/env bash
# DOG CYCLE 9 revert-diagnosis stand: the tree WITHOUT the cycle-9 change
# (DogStanceController.cs and DogTrialRecorder.cs at HEAD f8e41d2d), built on the
# CURRENT tree so the only difference from the cycle-9 build is the change
# itself. One quiet + one walk run, to separate "my change moves the quiet
# trace" from "the tree moved it" (Assets/TrialRecorder.cs 00:19 and
# Assets/BalanceControllerCaptureWalk.cs 00:20 landed after the 23:33 baseline
# player).
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MANIFEST="$PROJECT/Build/Headless/build-manifest.json"
OUT="$PROJECT/Trials/Runs"
LOGDIR="$PROJECT/Logs"
FP=$(grep -o '"fingerprint"[^,]*' "$MANIFEST" | cut -d'"' -f4)
echo "FP=$FP"
"$EXE" -batchmode -trial -nographics -duration 25 -label dog_c9r_q \
  -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
  -fixedDelta 0.005 -dog 1 > "$LOGDIR/c9r-dog_c9r_q.log" 2>&1
echo "RUN dog_c9r_q exit=$?"
"$EXE" -batchmode -trial -nographics -duration 25 -label dog_c9r_w1 \
  -out "$OUT" -sourceFingerprint "$FP" -buildManifest "$MANIFEST" \
  -fixedDelta 0.005 -dog 1 -dogWalk 1 -dogWalkTime 1 > "$LOGDIR/c9r-dog_c9r_w1.log" 2>&1
echo "RUN dog_c9r_w1 exit=$?"
md5sum "$OUT/dog_c9r_q.csv" "$OUT/dog_c9r_w1.csv"
echo DONE
