#!/usr/bin/env bash
# BALANCE-ARENA-1 grid video: the labelled takes at the ENDS of the card's ranges.
#
# Three takes, one per shape, each at a corner of the card's own ranges:
#   imp_g1L  circle 0.25 m  0.5 kg  20 m/s  - the lightest object at the highest speed
#   imp_g2L  square 0.30 m 15   kg  20 m/s  - the heaviest object at the highest speed
#   imp_g3L  rect   0.60 m 15   kg   4 m/s  - the heaviest object at the lowest speed
#
# `-visual` is a second per-frame camera render on top of the simulation, and the
# published note holds: only `-nographics` reproduces bit-for-bit, so these takes are
# evidence of the LOOK, and the numbers they carry are the take's own numbers. Each
# take draws its own caption (Assets/ImpactCaption.cs), so the video says what hit the
# man without a subtitle file.
#
# usage: bash Tools/impact-grid-video.sh
set -u
cd "E:/Games/2D Game/My project"
RUNS="Trials/Runs"

run() {
  local label="$1"; shift
  echo "=== $label"
  bash Tools/impact-run.sh "$label" 24 --visual "$@" || echo "RUN FAILED $label"
}

run imp_g1L -impact 1 -impactCount 3 -impactSeed 1 -impactShape circle -impactSize 0.25 \
  -impactMass 0.5 -impactSpeed 20 -impactAim torso -shotInterval 0.05
run imp_g2L -impact 1 -impactCount 3 -impactSeed 1 -impactShape square -impactSize 0.30 \
  -impactMass 15 -impactSpeed 20 -impactAim torso -shotInterval 0.05
run imp_g3L -impact 1 -impactCount 3 -impactSeed 1 -impactShape rect -impactSize 0.60 \
  -impactMass 15 -impactSpeed 4 -impactAim torso -shotInterval 0.05

# The mid-range labelled cut from the labels cycle is the previous evidence; keep it
# under its own name before impact-video.py rewrites impact_three_throws.mp4.
if [ -f "$RUNS/impact_three_throws.mp4" ] && [ ! -f "$RUNS/impact_three_throws_midrange.mp4" ]; then
  cp "$RUNS/impact_three_throws.mp4" "$RUNS/impact_three_throws_midrange.mp4"
  echo "kept the mid-range cut as impact_three_throws_midrange.mp4"
fi

python F:/hermes-data/scripts/impact-video.py \
  "imp_g1L=circle 0.25 m 0.5 kg 20 m/s -> torso" \
  "imp_g2L=square 0.30 m 15 kg 20 m/s -> torso" \
  "imp_g3L=rect 0.60 m 15 kg 4 m/s -> torso"

if [ -f "$RUNS/impact_three_throws.mp4" ]; then
  mv "$RUNS/impact_three_throws.mp4" "$RUNS/impact_grid_ends.mp4"
  echo "concat cut -> Trials/Runs/impact_grid_ends.mp4"
fi
echo "VIDEO DONE"
