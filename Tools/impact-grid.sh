#!/usr/bin/env bash
# BALANCE-ARENA-1 grid runner (level worker, 19.09.2026).
#
# The card (`.cursor/tasks/BALANCE-ARENA-1.md`) orders `-impactMass` 0.5-15 kg,
# `-impactSpeed` 4-20 m/s and three shapes as INDEPENDENT switches. The published
# runs only ever used three mid-range triples, so nothing had ever touched the ends
# of those ranges. This script walks the four corners of (mass, speed) for each
# shape, one throw per run so one row = one class:
#
#     <shape> x {0.5 kg, 15 kg} x {4 m/s, 20 m/s} = 12 runs
#
# Aim, side and the throw time are the scene's own seed-driven defaults (seed 1,
# aim torso, side seed, first throw 8 s): the grid isolates mass, speed and shape,
# and the seed jitter itself is already covered by the published imp_seedA/imp_seedB
# pair. `-nographics` only: that is the switch set the card's rule zero reproduces
# bit-for-bit, so every row here is comparable with every other row.
#
# usage: bash Tools/impact-grid.sh [--visual]   (--visual also cuts the video takes)
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

# --- rule zero: -impactCount 0 must reproduce the quiet stand ------------------
run imp_quiet_grid 40 -impact 1 -impactSeed 1 -impactCount 0

# --- the grid -----------------------------------------------------------------
for i in 0 1 2; do
  shape="${SHAPES[$i]}"; size="${SIZES[$i]}"
  for mass in "${MASSES[@]}"; do
    for speed in "${SPEEDS[@]}"; do
      m=$(echo "$mass" | tr '.' 'p')
      label="grid_${shape}_${m}_${speed}"
      run "$label" 16 -impact 1 -impactCount 1 -impactSeed 1 \
        -impactShape "$shape" -impactSize "$size" \
        -impactMass "$mass" -impactSpeed "$speed" -impactAim torso
    done
  done
done

# --- the labelled video takes (one take each, -visual is not bit-reproducible) --
if [ "${1:-}" = "--visual" ]; then
  run imp_g1L 24 -impact 1 -impactCount 3 -impactSeed 1 -impactShape circle \
    -impactSize 0.25 -impactMass 0.5 -impactSpeed 20 -impactAim torso -visual -shotInterval 0.05
  run imp_g2L 24 -impact 1 -impactCount 3 -impactSeed 1 -impactShape square \
    -impactSize 0.30 -impactMass 15 -impactSpeed 20 -impactAim torso -visual -shotInterval 0.05
  run imp_g3L 24 -impact 1 -impactCount 3 -impactSeed 1 -impactShape rect \
    -impactSize 0.60 -impactMass 15 -impactSpeed 4 -impactAim torso -visual -shotInterval 0.05
fi
echo "GRID DONE"
