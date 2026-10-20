#!/usr/bin/env bash
# DOG CYCLE 6 visual stand: ONE rendered run on the cycle-6 player. The batch
# runner always passes -nographics and the player then logs "visual skipped", so
# the player is invoked directly without it. Frames are compared byte for byte
# with the accepted state's frames (cycle 3 / cycle 2), and the clip is built
# from the run's own label.
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MAN="$PROJECT/Build/Headless/build-manifest.json"
FP=$(grep -o '"fingerprint"[^,]*' "$MAN" | cut -d'"' -f4)
echo "FP=$FP"
"$EXE" -batchmode -trial -duration 25 -label dog_c6_vis1 -out "$PROJECT/Trials/Runs" \
  -sourceFingerprint "$FP" -buildManifest "$MAN" -fixedDelta 0.005 \
  -dog 1 -dogWalk 1 -dogWalkTime 1 -visual \
  > "$PROJECT/Logs/stand-dog_c6_vis1.log" 2>&1
echo "RUN exit=$?"
echo "visual skipped lines: $(grep -a -c 'visual skipped' "$PROJECT/Logs/stand-dog_c6_vis1.log")"
cd "$PROJECT/Trials/Runs" || exit 1
n=$(ls shot_dog_c6_vis1_*.png 2>/dev/null | wc -l)
echo "frames written: $n"
same=0; diff=0
for f in shot_dog_c3_vis1_*.png; do
  b=${f/shot_dog_c3_vis1_/}
  if cmp -s "$f" "shot_dog_c6_vis1_$b" 2>/dev/null; then same=$((same+1)); else diff=$((diff+1)); fi
done
echo "vs accepted (cycle-3) frames: identical $same, differing $diff"
md5sum dog_c6_vis1.csv | cat
bash "$LOCALAPPDATA/hermes/scripts/make-preview.sh" dog_c6_vis1 5 2>&1 | tail -3
ls -la "$PROJECT/Trials/Runs/dog_c6_vis1.mp4" 2>&1
