#!/usr/bin/env bash
# The closing-speed cycle's acceptance script (level worker, 19.09.2026).
#
#   1. rule zero      : close_quiet (-impactCount 0, 40 s) vs the accepted quiet stand
#   2. the grid       : close_* vs cv_grid_* (the same source lineage, my change isolated)
#                       and vs grid_*   (the card's accepted 12-class grid)
#   3. the fixed row  : the class the old metric lied about, both directions
#
# usage: bash Tools/impact-closing-accept.sh
set -u
cd "E:/Games/2D Game/My project"
SHAPES=(circle square rect)
SIZES=(0.25 0.30 0.60)
MASSES=(0.5 15)
SPEEDS=(4 20)

classes=()
for shape in "${SHAPES[@]}"; do
  for mass in "${MASSES[@]}"; do
    m=$(echo "$mass" | tr '.' 'p')
    for speed in "${SPEEDS[@]}"; do
      classes+=("${shape}_${m}_${speed}")
    done
  done
done

pairs() {
  local prefix="$1"; shift
  local out=()
  for c in "${classes[@]}"; do out+=("close_$c" "${prefix}_$c"); done
  python Tools/impact-closing-table.py "${out[@]}"
}

echo "### rule zero: -impactCount 0 vs the accepted quiet stand"
python Tools/impact-json-diff.py Trials/Runs/imp_quiet40.json Trials/Runs/close_quiet.json | head -20
echo
echo "### rule zero vs the previous build's quiet run (cv_quiet)"
python Tools/impact-json-diff.py Trials/Runs/cv_quiet.json Trials/Runs/close_quiet.json | head -20
echo
echo "### THE GRID, compared with the previous build (cv_grid_*)"
pairs cv_grid
echo
echo "### THE GRID, compared with the card's accepted grid (grid_*)"
pairs grid
echo
echo "### the class the old metric lied about"
python Tools/impact-closing-table.py close_rect_15_4 cv_grid_rect_15_4
python Tools/impact-closing-table.py close_rect_15_4 grid_rect_15_4
