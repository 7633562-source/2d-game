#!/usr/bin/env bash
# One-species scene capture (Yury 18.09: a scene per species, nothing else in it).
#
# Plays the scene in the art preview player and writes the still plus the counter
# JSON. It never builds: the player comes from
#   Unity.exe -batchmode -nographics -projectPath "$PROJ" \
#     -executeMethod GalleryPreviewBuild.Build -logFile Logs/art1-species-build.log -quit
#
#   bash Tools/species-run.sh treeoak TreeOak
#   bash Tools/species-run.sh treepine TreePine
#   bash Tools/species-run.sh treeapple TreeApple     # the control, same build
#
# Acceptance read from the log/JSON: treeCount 1, fits true, missFrames 0.
set -uo pipefail

PROJ="E:/Games/2D Game/My project"
EXE="$PROJ/Build/Preview/GalleryPreview.exe"
RUNS="$PROJ/Trials/Runs"

label="${1:?usage: species-run.sh <label> <scene>}"
scene="${2:?usage: species-run.sh <label> <scene>}"
log="$PROJ/Logs/art1-$label.log"

if [ ! -x "$EXE" ]; then
  echo "FAIL no preview player at $EXE - build it first" >&2
  exit 1
fi

"$EXE" -batchmode -scene "$scene" -capture 1 -label "$label" -out "$RUNS" \
  -w 1920 -h 1080 -observe 6 -logFile "$log"
rc=$?

grep -a "SPECIES-ARGS\|SPECIES-DRESS\|SPECIES-CAPTURE" "$log" | tail -6
python - "$RUNS" "$label" <<'PY'
import json, sys, pathlib
runs, label = sys.argv[1], sys.argv[2]
p = pathlib.Path(runs) / (label + "-tree.json")
if not p.exists():
    print("FAIL no counter JSON at", p); raise SystemExit(2)
d = json.loads(p.read_text(encoding="utf-8-sig"))
print("SPECIES-ACCEPT label=%s scene=%s species=%s trees=%d (plant %d / apple %d) "
      "height=%.3f m ortho=%.3f fits=%s missFrames=%d warmup=%d dressOutside=%.3f m "
      "firstMiss=%s (outside %.3f m) missAfter1s=%d boundsGrowth=%.3f m observedFrames=%d "
      "viewportX=[%.3f,%.3f] viewportY=[%.3f,%.3f]"
      % (d["label"], d["scene"], d["species"], d["treeCount"], d["plantTreeCount"],
         d["appleTreeCount"], d["heightMeters"], d["orthoSize"], d["fits"],
         d["missFrames"], d.get("warmupFrames", -1), d.get("dressOutsideMeters", -1),
         d.get("firstMissSeconds", -1), d.get("firstMissOutsideMeters", -1),
         d.get("missFramesAfterOneSecond", -1), d.get("boundsGrowthMeters", -1),
         d["observedFrames"], d["minViewportX"], d["maxViewportX"],
         d["minViewportY"], d["maxViewportY"]))
ok = d["treeCount"] == 1 and d["fits"] and d.get("missFramesAfterOneSecond", 1) == 0
print(("SPECIES-ACCEPT OK" if ok else "SPECIES-ACCEPT FAIL") + " " + label)
raise SystemExit(0 if ok else 1)
PY
verdict=$?

photo="$RUNS/${label}_photo.png"
echo "photo: $photo ($(stat -c%s "$photo" 2>/dev/null || echo missing) bytes)"
[ $rc -eq 0 ] && [ $verdict -eq 0 ]
