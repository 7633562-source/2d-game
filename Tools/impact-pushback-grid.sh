#!/usr/bin/env bash
# BALANCE-ARENA-1 push-back cycle (level worker, 19.09.2026).
#
# The card asks, per throw, "whether the rig pushed back at all (vertical force above
# body weight after the impact, with the frame number where it happens)". The grid
# audit (`Trials/Runs/impact-grid-report.md`, section 5c) proved the old column set
# `pushedBack` on the collision itself: true in 12/12 classes, 0..5 ms after contact.
# This cycle separates the collision from the body's answer, so the grid is re-run to
# fill the new columns.
#
# Same switch set as `Tools/impact-grid.sh` (the card's rule zero and the four corners
# of shape x mass x speed), but under the label prefix `pb_` so the accepted evidence of
# the previous build is NOT overwritten: the two builds can be compared file by file.
#
# usage: bash Tools/impact-pushback-grid.sh [--quiet-only]
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

run pb_quiet 40 -impact 1 -impactSeed 1 -impactCount 0
if [ "${1:-}" = "--quiet-only" ]; then echo "QUIET DONE"; exit 0; fi

for i in 0 1 2; do
  shape="${SHAPES[$i]}"; size="${SIZES[$i]}"
  for mass in "${MASSES[@]}"; do
    for speed in "${SPEEDS[@]}"; do
      m=$(echo "$mass" | tr '.' 'p')
      label="pb_grid_${shape}_${m}_${speed}"
      run "$label" 16 -impact 1 -impactCount 1 -impactSeed 1 \
        -impactShape "$shape" -impactSize "$size" \
        -impactMass "$mass" -impactSpeed "$speed" -impactAim torso
    done
  done
done
echo "PUSH-BACK GRID DONE"
