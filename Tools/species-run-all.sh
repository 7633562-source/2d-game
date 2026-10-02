#!/usr/bin/env bash
# Every one-species scene, one run each (Yury 18.09: a scene per species, nothing else in it).
#
# Plays each scene in the art preview player and writes its still + counter JSON.
# This never builds: the player comes from
#   Unity.exe -batchmode -nographics -projectPath "$PROJ" \
#     -executeMethod GalleryPreviewBuild.Build -logFile Logs/art1-species-build.log -quit
#
#   bash Tools/species-run-all.sh              # apple + all nine cloned species
#   bash Tools/species-run-all.sh willow plum  # only these
#
# Exits non-zero if any scene fails species-run.sh's acceptance (treeCount 1,
# fits true, missFramesAfterOneSecond 0). apple is the control: same build,
# same protocol, already accepted in the earlier cycle.
set -uo pipefail

PROJ="E:/Games/2D Game/My project"
SPECIES=(apple oak pine willow bush poplar lilac hazel cedar plum)

if [ "$#" -gt 0 ]; then
  selected=("$@")
else
  selected=("${SPECIES[@]}")
fi

fails=0
for s in "${selected[@]}"; do
  scene="Tree$(python -c "import sys;print(sys.argv[1].capitalize())" "$s")"
  echo "=== $s -> $scene ==="
  bash "$PROJ/Tools/species-run.sh" "tree$s" "$scene" || fails=$((fails + 1))
done

echo "SPECIES-RUN-ALL scenes=${#selected[@]} failed=$fails"
[ "$fails" -eq 0 ]
