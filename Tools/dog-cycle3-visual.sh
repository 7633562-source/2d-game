#!/usr/bin/env bash
# DOG cycle 3 visual stand: ONE rendered run on the current player (NO -nographics,
# the runner always passes it and the player then logs "visual skipped"), then the
# captured frames are compared byte-for-byte with the accepted dog_c2_vis1 frames.
set -u
PROJECT="E:/Games/2D Game/My project"
EXE="$PROJECT/Build/Headless/BalanceTrial.exe"
MAN="$PROJECT/Build/Headless/build-manifest.json"
FP=$(grep -o '"fingerprint"[^,]*' "$MAN" | cut -d'"' -f4)
echo "FP=$FP"
"$EXE" -batchmode -trial -duration 25 -label dog_c3_vis1 -out "$PROJECT/Trials/Runs" \
  -sourceFingerprint "$FP" -buildManifest "$MAN" -fixedDelta 0.005 \
  -dog 1 -dogWalk 1 -dogWalkTime 1 -visual \
  > "$PROJECT/Logs/stand-dog_c3_vis1.log" 2>&1
echo "RUN exit=$?"
grep -a -c "visual skipped" "$PROJECT/Logs/stand-dog_c3_vis1.log"
cd "$PROJECT/Trials/Runs" || exit 1
n=$(ls shot_dog_c3_vis1_*.png 2>/dev/null | wc -l)
echo "new frames: $n (reference dog_c2_vis1: $(ls shot_dog_c2_vis1_*.png | wc -l))"
same=0; diff=0
for f in shot_dog_c2_vis1_*.png; do
  b=${f/shot_dog_c2_vis1_/}
  if cmp -s "$f" "shot_dog_c3_vis1_$b"; then same=$((same+1)); else diff=$((diff+1)); echo "DIFF frame $b"; fi
done
echo "frames byte-identical: $same, differing: $diff"
md5sum dog_c3_vis1.csv dog_c2_vis1.csv | cat
