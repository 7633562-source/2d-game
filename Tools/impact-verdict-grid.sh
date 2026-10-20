#!/usr/bin/env bash
# BALANCE-ARENA-1 verdict cycle (level worker, 19.09.2026).
#
# The card asks the per-throw record for `fell` = "the shared FallCriterion AND the
# controller's own state". The grid audit (Trials/Runs/impact-grid-report.md, 5e) found
# the row carried the criterion alone: the controller's ladder was available in the walk
# harness (ctrlFallState*) and was not wired into the impact row. This cycle wires it, so
# the grid is re-run to fill the new columns.
#
# Same twelve classes and the same switch set as Tools/impact-pushback-grid.sh (rule zero
# + the four corners of shape x mass x speed), under the label prefix `cv_` so the accepted
# evidence of the two earlier builds is NOT overwritten: the three builds can be compared
# class by class.
#
# usage: bash Tools/impact-verdict-grid.sh [--quiet-only]
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

run cv_quiet 40 -impact 1 -impactSeed 1 -impactCount 0
if [ "${1:-}" = "--quiet-only" ]; then echo "QUIET DONE"; exit 0; fi

for i in 0 1 2; do
  shape="${SHAPES[$i]}"; size="${SIZES[$i]}"
  for mass in "${MASSES[@]}"; do
    for speed in "${SPEEDS[@]}"; do
      m=$(echo "$mass" | tr '.' 'p')
      label="cv_grid_${shape}_${m}_${speed}"
      run "$label" 16 -impact 1 -impactCount 1 -impactSeed 1 \
        -impactShape "$shape" -impactSize "$size" \
        -impactMass "$mass" -impactSpeed "$speed" -impactAim torso
    done
  done
done
echo "VERDICT GRID DONE"
