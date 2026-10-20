#!/usr/bin/env bash
# BALANCE-ARENA-1 closing-speed cycle runner (level worker, 19.09.2026).
#
# Same 12 classes as Tools/impact-grid.sh, same switch set, new labels (`close_*`), so
# every row can be diffed against its accepted `grid_*` twin on the SAME build:
#
#   * rule zero   : -impactCount 0, 40 s  -> close_quiet
#   * the grid    : `<shape> x {0.5, 15} kg x {4, 20} m/s`, one throw per run
#
# usage: bash Tools/impact-closing-grid.sh [--visual]
set -u
cd "E:/Games/2D Game/My project"

SHAPES=(circle square rect)
SIZES=(0.25 0.30 0.60)
MASSES=(0.5 15)
SPEEDS=(4 20)

run() {
  local label="$1" duration="$2"; shift 2
  echo "=== $label"
  bash Tools/impact-run.sh "$label" "$duration" "$@" || echo "RUN FAILED $label"
}

run close_quiet 40 -impact 1 -impactSeed 1 -impactCount 0

for i in 0 1 2; do
  shape="${SHAPES[$i]}"; size="${SIZES[$i]}"
  for mass in "${MASSES[@]}"; do
    for speed in "${SPEEDS[@]}"; do
      m=$(echo "$mass" | tr '.' 'p')
      label="close_${shape}_${m}_${speed}"
      run "$label" 16 -impact 1 -impactCount 1 -impactSeed 1 \
        -impactShape "$shape" -impactSize "$size" \
        -impactMass "$mass" -impactSpeed "$speed" -impactAim torso
    done
  done
done

# One labelled video take of the class the old metric lied about, and one of the class
# the slow-moving objects come from (-visual is not bit-reproducible with -nographics).
if [ "${1:-}" = "--visual" ]; then
  run imp_c1L 24 -impact 1 -impactCount 3 -impactSeed 1 -impactShape rect \
    -impactSize 0.60 -impactMass 15 -impactSpeed 4 -impactAim torso -visual -shotInterval 0.05
  run imp_c2L 24 -impact 1 -impactCount 3 -impactSeed 1 -impactShape circle \
    -impactSize 0.25 -impactMass 0.5 -impactSpeed 20 -impactAim torso -visual -shotInterval 0.05
fi
echo "CLOSING GRID DONE"
